# ADR-003: Governed Content Studio and draft-only AI authoring

- Status: Accepted
- Date: 2026-08-13
- Decision owners: René Brauwers and the reneb.au maintainers
- Supersedes: the static-homepage part of ADR-001; recruiter identity, privacy and deployment boundaries remain unchanged

## Context

The portfolio and recruiter portal had two editorial paths: a static homepage in the gateway image and specialist public/private profile forms in the portal. Homepage/footer/privacy/discovery copy could not be managed consistently, the Opportunity profile editor was difficult to find, generated `llms.txt`/Markdown/JSON surfaces could drift if treated as independent files, and Umami settings were compiled into markup. René also needs optional LLM assistance using OpenRouter or xAI without giving a model private-data access or publication authority.

A general CMS such as Orchard Core would duplicate the established Razor Pages authentication, TOTP roles, encrypted SQLite storage, publishing and deployment model. Arbitrary HTML or page-builder capability would enlarge the XSS, privacy and operational surface for a small, fixed route set.

## Decision

1. ASP.NET Core remains the only application runtime and SQLite remains the only database. Nginx is still the sole exposed container and serves CSS/images/icons directly, but proxies `/` and generated discovery routes to Razor Pages.
2. Six encrypted `ContentDocument` records own homepage, global/Umami settings, recruiter profile, private opportunity profile, privacy notice and machine-discovery guidance. Each has draft/published pointers, immutable revisions, optimistic concurrency, preview/diff, recent-TOTP publish/rollback and a 20-revision limit.
3. Narrative fields use self-hosted Quill 2 Delta with an allowlisted semantic renderer. Facts, lists, dates, links, compensation and Umami settings remain typed inputs. Editor HTML, embeds, images, styles and unknown Delta operations/attributes are rejected server-side.
4. `llms.txt`, recruiter Markdown, candidate JSON and JSON-LD are generated from the published recruiter profile plus guidance. Robots and sitemap responses are generated from the route/publication contract. They are previewable but not separately editable.
5. Umami is configured through typed site settings: enable flag, plain HTTPS `.js` URL, UUID website ID, domain list, exclude-search and Do-Not-Track flags. Arbitrary script fragments are not accepted. CSP is calculated from the published script origin. Analytics remain prohibited on privacy, auth, portal and admin routes.
6. OpenRouter and xAI implement a provider-neutral authoring interface without automatic fallback. One credential per provider is encrypted with a separate host-mounted AI keyring. A provider remains unusable until authenticated discovery, compatible model selection, strict structured-output testing and cost controls pass.
7. Calls are host-disabled by default. OpenRouter requires `require_parameters=true` and `data_collection=deny`; xAI uses Responses with `store=false` and records the returned ZDR header. Per-provider caps, a site-wide monthly ceiling, pre-call reservation and post-call reconciliation include provider-billed invalid results.
8. AI receives only explicitly selected published content. Private opportunity data, the active résumé and validated context uploads require per-request disclosure acknowledgement. Recruiters, messages, credentials, TOTP, mail and audit data are never selectable. Upload text is untrusted evidence, models receive no tools or browsing, and proposals may only update a non-stale draft. Human TOTP publication is separate.
9. Provider keys, content revisions, conversations, messages, proposals, context originals/extracted text/filenames and private profiles are encrypted. Conversations expire after 30 inactive days; context persists until explicit deletion. Metadata-only usage/audit records contain no prompts or responses.

## Alternatives considered

- Keep the static homepage and add more specialist forms: rejected because duplicate content paths and generated surfaces would continue to drift.
- Adopt Orchard Core or another full CMS: rejected as disproportionate duplication of identity, roles, persistence and publishing for six fixed documents.
- Store arbitrary HTML from a WYSIWYG editor: rejected due to avoidable stored-XSS and machine-rendering risk.
- Let AI publish directly or browse/use tools: rejected because content claims, private disclosure and publication require human accountability.
- Put provider keys in `.env`: rejected because administrators need governed replacement/deletion and the credentials require their own rotation boundary.

## Consequences

Positive:

- René can edit all intended content, Opportunity details and safe analytics settings from one navigable studio.
- Public representations remain consistent by construction and private values have a server-side authentication boundary.
- Draft history, diffs, previews and human publication make AI assistance reviewable and reversible.
- Production remains pull-only and source-free with the same two-image topology.

Costs and risks:

- The portal is now required for the homepage; readiness, migration and matching-image deployment are therefore critical to public availability.
- Quill, PDF/DOCX extraction and two provider contracts add dependencies and test surface.
- Restores containing provider settings require both field and AI credential keyrings.
- External provider retention and pricing remain third-party facts; the UI must disclose observed/current posture and administrators must re-test after key/model changes.

## Mitigations and follow-up

- Keep public output JavaScript-optional, add gateway/portal health checks and deploy matching immutable image tags.
- Contract-test provider request/response/error shapes with fake services and re-check official documentation during dependency/provider updates.
- Fuzz Delta and file inputs, scan anonymous responses/images/layers/logs for private markers, and run authenticated mobile/desktop visual acceptance.
- Start with `AI_EGRESS_ENABLED=false`; enable only after keyring, migration, content and provider/cost-control acceptance.
- Use expand/contract schema changes and preserve legacy candidate dual-writes during the rollback window.
