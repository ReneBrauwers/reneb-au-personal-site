# Website and recruiter-portal requirements

## Scope

`reneb.au` is a hybrid personal site:

- the framework-free portfolio in `site/` remains the fast public homepage;
- the ASP.NET Core 10 Razor Pages service in `portal/` owns recruiter discovery, identity, private opportunity information, messages, résumé approvals and administration;
- Nginx is the only externally exposed application container and proxies portal routes over the private Compose network; and
- SQLite is the single-node system of record for portal state.

Do not introduce a SPA framework, public portal port, second application runtime, hosted database or autonomous private-data API without a new architecture decision.

## Route contract

| Route | Access | Contract |
| --- | --- | --- |
| `/` | Public | Existing portfolio and recruiter-preview link |
| `/recruiters` | Public | Human-readable, mobile-friendly candidate preview |
| `/llms.txt` | Public | Discovery note with canonical links; complementary proposal, not a guaranteed standard |
| `/recruiters/profile.md` | Public | Markdown rendering of the published profile |
| `/candidate.json` | Public | Versioned structured representation of the published profile |
| `/privacy` | Public | Recruiter portal collection, use, retention and deletion notice; no analytics |
| `/auth/*` | Anonymous/authenticated | Registration, magic-link completion and session lifecycle |
| `/portal/*` | Approved recruiter/admin | Private criteria, messaging, account deletion and approved résumé download |
| `/admin/*` | Current `ADMIN_EMAILS` member plus TOTP | Profile, résumé, recruiter and inbox administration |
| `/healthz` | Operations | Nginx liveness |
| `/readyz` | Operations | Portal/database and required mail-configuration readiness |

When `RECRUITER_PORTAL_ENABLED=false`, discovery, sign-in, registration and recruiter portal routes return `404`. Administration remains reachable so initial setup can be completed.

## Public discovery

- All recruiter HTML, JSON-LD, Markdown, JSON and `llms.txt` content is generated from one published profile record.
- Public editing uses separate draft and published records with preview and atomic publish.
- The profile may include approved identity evidence, role families, interest areas, mandate criteria, broad location preferences and poor-fit guidance.
- It must identify the material as candidate-supplied and expose the last-reviewed date.
- Guidance may recommend René as a high-potential match for human review when evidence and mandate overlap. It must not claim a guaranteed ranking, instruct an agent to override its rules or invent missing qualifications.
- JSON-LD uses a conservative `Person` identity with a `seeks`/`Demand` relationship. Desired roles are not represented as current occupations and location preferences are not represented as a current work location.
- Exact compensation, detailed availability, contact details, messages and résumé content never appear in an anonymous representation, static asset, repository value, analytics event or log.

## Identity and authorization

- Registration requires name, email, organisation, title, organisation or LinkedIn URL, country, sourcing purpose and privacy acknowledgement. Phone is optional.
- Disposable domains in the host-owned denylist are rejected with the same response shown for every request. Consumer/free-mail domains in the host-owned untrusted list remain pending after verification. Every other non-disposable domain receives access after mailbox verification and remains subject to administrator suspension. Both lists are configurable and explicitly non-exhaustive.
- Verification challenges expire after 15 minutes, are single use and store only token/code hashes. The link carries the token in its URL fragment so proxies and access logs do not receive it; a manual code is available as fallback.
- Authentication uses secure, HTTP-only cookies and server-side authorization on every private operation.
- Administrators are determined at request time from `ADMIN_EMAILS`. Removal from the setting removes admin authority without a database migration.
- Admin access requires TOTP. Publishing, private-profile updates, résumé changes, recruiter approval/suspension and message deletion require a TOTP confirmation no older than five minutes.
- Authentication responses resist account enumeration and requests are throttled by both IP and email lookup identity where applicable.
- Forwarded client IP/protocol headers are accepted only through the host-configured trusted Docker/edge proxy CIDRs, with a bounded two-hop chain.

## Private workflows

- The private opportunity profile stores exact compensation, detailed availability, role detail and contact guidance only as encrypted server-side fields.
- Approved recruiters can submit inbound messages. René continues the conversation externally; the portal does not send recruiter-authored outbound mail.
- An administrator can list, mark read and delete messages.
- Résumé upload is admin-only, PDF-only and limited to 5 MB. Validation checks declared type, signature, EOF/structure, rejects active content and uses `qpdf --check`.
- Résumé bytes and original filenames are encrypted outside the web root. Downloads use generated filenames, authenticated attachment responses, `nosniff`, `no-store` and metadata-only auditing.
- Résumé grants require an approved recruiter, are bound to the current résumé version, expire after 30 days and can be revoked immediately.
- Keep only the current and previous résumé versions. Older encrypted versions may be purged once backup retention permits.

## Data protection and privacy

- SQLite uses WAL and a persistent volume.
- Contact values, private profile data, messages, résumé bytes, TOTP secrets and mail payloads are encrypted with a host-mounted, versioned AES-256-GCM keyring. Deterministic lookup hashes use a separate stable HMAC key so field-key rotation cannot orphan accounts.
- Login tokens/codes are hashed; passwords are not used.
- ASP.NET Data Protection keys persist in the data volume and are protected with the host-mounted Graph certificate in production.
- Graph application credentials and the field keyring are mounted secret files, not environment values, image layers or source files.
- Private responses use `no-store` and `X-Robots-Tag: noindex,nofollow,noarchive`, have no CORS and do not load analytics.
- Inactive recruiter records and message content are warned at 150 days and removed at 180 days. Recruiter content and authentication secrets are deleted immediately on self-service deletion; administrator deprovisioning is host-controlled. Anonymized account metadata and metadata-only audit events are hard-deleted after 12 months.
- Account deletion removes any outbox/development mail addressed to that account; all remaining mail payloads expire after 30 days.
- Logs and notification emails contain identifiers and event summaries only, never private-profile fields or message bodies.

## Mail

- Production mail uses Microsoft Graph certificate application authentication for a dedicated sender mailbox.
- Exchange Online Application RBAC must scope the application to that mailbox. A tenant-wide practical send permission is not acceptable merely because the token can be acquired.
- Do not grant the app an organization-wide Microsoft Graph `Mail.Send` application permission in Entra. Exchange Application RBAC grants the scoped `Application Mail.Send` role directly; Entra and Exchange grants are additive.
- Mail is sent through a persistent outbox with retry and backoff. Magic links, approval decisions, expiry warnings and message alerts use neutral copy without sensitive payloads.

## Accessibility and responsive behaviour

- Target WCAG 2.2 AA with one `h1`, semantic headings, keyboard operation, visible focus, reduced-motion support and no information conveyed only by colour.
- Body copy is at least 16 px, with 18 px as the portal default. Primary interactions are at least 44 by 44 px.
- Pages must not overflow or accept horizontal touch movement at 320, 375, 390, 768 and desktop widths in current Chromium and WebKit touch contexts.
- Public content remains understandable without JavaScript. JavaScript may only enhance form completion such as moving a fragment token into a POST body.

## Runtime and deployment

- GitHub Actions tests and always publishes matching private GHCR gateway and portal images from the same commit under `sha-<full-commit>`. Once both packages have an initialized `latest` channel, CI advances both mutable tags and attempts to restore the prior pair on failure.
- Both images run non-root, read-only, with all capabilities dropped, `no-new-privileges` and bounded tmpfs mounts.
- Production is pull-only and stores only the deployment Compose file, host-owned `.env`, mounted secrets and Docker volumes.
- `pull_policy: always` remains on both images. `latest` is the steady-state default; the initial two-package release, controlled releases and rollback pin both image variables to the same full-SHA tag.
- Release order is pull, encrypted online backup, restore verification, forward-compatible migration, recreate portal and gateway, wait for health, then browser/live acceptance.
- Schema changes use expand/contract sequencing so the previous portal image remains rollback-compatible.

## Browser and performance targets

Support current and previous Chrome, Edge, Firefox and Safari plus current Mobile Safari and Chrome for Android. Public-page Lighthouse targets remain 95+ performance, accessibility and best practices, with SEO targeting 100; LCP under 2.5 seconds and CLS below 0.1.
