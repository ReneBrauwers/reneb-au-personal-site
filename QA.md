# Quality assurance and definition of done

The implementing agent owns every applicable lane. A code review or desktop screenshot alone is not acceptance.

## Local commands

```sh
docker compose build web portal
docker run --rm --volume "$PWD:/src" --workdir /src mcr.microsoft.com/dotnet/sdk:10.0.400-noble dotnet test portal.tests/ReneB.Portal.Tests.csproj --configuration Release
docker compose --profile qa build qa
docker compose --profile qa run --rm qa npm run html
docker compose --profile qa run --rm qa npm run css
docker compose --profile qa run --rm qa npm run validate
docker compose --profile qa run --rm qa npm run lighthouse
```

Use the digest-pinned SDK reference from the workflow when reproducing CI exactly. Browser evidence is written beneath ignored `artifacts/`.
`QA_MAIL_URL` must identify the Development-mode mailbox endpoint for the same stack as `QA_BASE_URL`; it is never available in production.

## Public content and discovery

- [ ] Approved portfolio claims remain accurate and understated.
- [ ] `/recruiters`, `/llms.txt`, `/recruiters/profile.md` and `/candidate.json` contain the same identity, evidence, role interests, locations, fit/non-fit signals, canonical recruiter URL, candidate-supplied disclosure and last-reviewed date.
- [ ] `candidate.json` has the documented schema version and JSON-LD parses as a `Person` identity with `seeks`/`Demand`.
- [ ] Desired roles are not presented as current employment; preferred locations are not presented as current work location.
- [ ] No surface claims a guaranteed AI rank or includes prompt-injection wording.
- [ ] `/privacy` is linked from registration, readable without authentication and loads no analytics.

## Leakage and privacy

Scan source, anonymous responses, static assets, sitemap, the application assembly/config and public files in both final image filesystems/layers, and captured logs. Do not pattern-scan unrelated native dependency binaries as text.

- [ ] Exact compensation figures, detailed availability conditions, messages, recruiter PII and résumé bytes are absent.
- [ ] No tokens, manual codes, TOTP secrets, key material, Graph credentials, private hostnames or infrastructure addresses are present.
- [ ] Every `/auth`, `/portal` and `/admin` response/redirect uses `no-store` and `X-Robots-Tag: noindex,nofollow,noarchive`.
- [ ] Umami appears only on `/` and `/recruiters`, honours search/DNT exclusions and emits no private payloads.
- [ ] `/privacy` and all private routes make no Umami request.
- [ ] Registration provides a clear collection notice and self-service deletion is available after sign-in.

## Authentication and authorization

- [ ] Registration requires every contracted field and a privacy acknowledgement; repeat anonymous registration cannot rewrite or demote an existing account.
- [ ] Disposable domains are blocked, explicitly allowlisted business domains activate after mailbox verification, and free/unlisted domains remain pending.
- [ ] Generic responses do not reveal account existence or domain classification.
- [ ] Magic links/codes expire at 15 minutes, are single use, store only hashes and reject replay; disabled-mode gates also cover trailing-slash route variants.
- [ ] The URL fragment token reaches the server only through an antiforgery-protected POST; manual code fallback works.
- [ ] Authentication throttling covers IP and email identity; manual-code and TOTP attempts use persistent identity-bound lockout/backoff that cannot be bypassed by rotating IPs.
- [ ] Sessions expire, sign-out/revocation works and removed `ADMIN_EMAILS` lose authority immediately.
- [ ] Approved recruiters cannot access admin pages and pending/suspended/deleted accounts cannot access private data.
- [ ] Admin TOTP setup is not persisted until a valid code proves enrolment; verification and five-minute step-up protect every sensitive action.

## Messaging, résumé and retention

- [ ] Approved recruiters can create inbound messages and admins can list, read and delete them; résumé access requests are persistently deduplicated for 24 hours.
- [ ] Notifications contain no private profile or message body; transport timeouts advance retry/backoff state instead of starving the outbox.
- [ ] PDF upload rejects files over 5 MB, renamed/non-PDF, malformed, encrypted/uninspectable, forms/XFA, rich media and other active-content structures.
- [ ] Successful download requires an active approved session plus a current, unexpired, unrevoked grant; headers force a non-cacheable attachment with `nosniff`, and a metadata-only download audit event is written.
- [ ] Grant expiry/revocation fails immediately and changing the current résumé invalidates an older-version grant.
- [ ] Current and newest previous résumé versions are retained indefinitely for rollback; only older versions are removed after the encrypted-backup retention window.
- [ ] Retention tests exercise the 150-day warning, 180-day content deletion, recruiter-only self-service deletion, immediate authentication-secret cleanup and 12-month hard deletion of residual metadata/audit records.

## Responsive, touch and accessibility

Automate and visually inspect 320×568, 375×667, 390×844, 768×1024 and 1440×900. Also inspect the homepage at 1024×768 and 1920×1080.

- [ ] No root/body horizontal overflow and forced horizontal movement remains zero in Chromium and WebKit iPhone touch contexts.
- [ ] Body text is at least 16 px (18 px portal default); controls are at least 44×44 px.
- [ ] One `h1`, semantic heading order, landmarks, meaningful labels and visible keyboard focus.
- [ ] No serious/critical axe issues; colour contrast meets WCAG 2.2 AA.
- [ ] 200% zoom, text reflow, forced colours, reduced motion and JavaScript-disabled reading are usable; the authenticated header and actions also pass at 320×568.
- [ ] Forms expose validation accessibly and do not lose entered context unnecessarily.

## Runtime and operations

- [ ] HTML/CSS validators and .NET tests pass; NuGet and npm dependency audits report no known high/critical vulnerability.
- [ ] Both images build from the same commit, run non-root/read-only and pass high/critical Trivy scans.
- [ ] CI emits SBOM/provenance and matching immutable full-SHA tags. It never auto-promotes a partially initialized `latest` channel; once both mutable tags exist, a promotion failure attempts to restore both previous pointers.
- [ ] Initial `latest` seeding requires an explicit `workflow_dispatch` with `initialize_latest=true`, promotes the absent tag first and leaves production pinned until both OCI revision labels match.
- [ ] GitHub's package API reports both GHCR packages as `private`, and a full anonymous pull of each immutable tag fails with an authorization-specific error; network, registry, rate-limit, missing-tag and other failures do not count as privacy proof, and the production account can pull both.
- [ ] Production contains only Compose, `.env`, secret files and Docker volumes—no repository, SDK, Dockerfile or QA/build tooling.
- [ ] Encrypted online backup succeeds, the newest backup passes restore/integrity verification, and migration succeeds before start.
- [ ] `web` and `portal` are recreated together so Nginx resolves the current portal container address.
- [ ] Both images report the intended matching revision/digest and services survive restart with sessions/data intact.
- [ ] `/healthz` and `/readyz` pass; unknown paths are real 404s; `/index.html` and public HTTP/www canonical redirects are correct.
- [ ] Live TLS, HSTS/CSP/security headers and external M365 delivery are verified.
- [ ] Full business/free/disposable-domain, message, résumé approval/download/revoke and account-deletion flows pass in a real browser.

## Release record

Handoff records the commit, both image digests, migration/backup result, tested viewports, screenshots, live acceptance evidence, rollback pair and any prerequisite left intentionally incomplete. Do not describe an untested external mail or TOTP flow as accepted.
