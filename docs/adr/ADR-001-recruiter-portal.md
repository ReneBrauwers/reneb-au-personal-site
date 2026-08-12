# ADR-001: Add an authenticated recruiter portal behind the static gateway

- Status: Accepted
- Date: 2026-08-12

## Context

The public portfolio needed to become discoverable to human-directed recruiting agents while presenting René as a credible high-potential match for relevant senior mandates. Publishing exact compensation, detailed availability, recruiter conversations or a résumé would create an unacceptable public privacy boundary. Hidden text or prompt injection would also be untrustworthy and likely discarded by responsible agents.

The existing production contract is a hardened private-GHCR Nginx image, external TLS proxy and pull-only host with no source checkout. The feature must preserve that boundary and remain usable at low single-node volume.

## Decision

Keep the framework-free portfolio and Nginx gateway. Add one non-root ASP.NET Core 10 Razor Pages service on the private Compose network and one SQLite database in a persistent volume.

Nginx remains the only exposed application container. It serves `/` and static assets directly, proxies public recruiter representations and every protected route, and preserves real 404 and canonical-index behaviour.

One published public profile record generates recruiter HTML, JSON-LD, Markdown, versioned JSON and `llms.txt`. A separate draft supports save, authenticated preview and atomic publish. Public guidance is candidate-supplied and evidence-led; it may recommend René for human review but cannot manipulate an agent or expose private terms.

The portal uses passwordless 15-minute, single-use mailbox challenges, an explicit trusted-business-domain allowlist with conservative pending approval for unknown domains, secure cookies backed by revocable server-side sessions, an `ADMIN_EMAILS` allowlist and TOTP step-up. Private profile data, recruiter PII, messages, résumé bytes, TOTP secrets and outbox payloads are encrypted with a host-mounted versioned AES-256-GCM keyring; a separate stable HMAC key preserves deterministic lookups through field-key rotation. Persisted ASP.NET Data Protection keys use a separate host-mounted certificate. Resume access is separately approved, time-limited and revocable. ADR-002 supersedes only this domain-classification decision and clarifies the scoped mail authorization mechanism.

Production mail uses Microsoft Graph certificate authentication and Exchange Online Application RBAC scoped to a dedicated mailbox. Production remains source-free and pull-only. CI publishes matching private gateway/portal images from the same commit under immutable full-SHA tags, with vulnerability gates, SBOM and provenance. The steady-state `latest` pair is advanced only after both immutable images pass, and only when both mutable tags already exist.

## Alternatives considered

### Publish all recruiter criteria publicly

Rejected. Public metadata cannot protect exact compensation, availability or the résumé, and public minimums can distort later negotiation.

### Hidden agent instructions or cloaking

Rejected. It is deceptive, fragile, hostile to crawler safety systems and provides no confidentiality.

### Client-only protected page

Rejected. Browser JavaScript, hidden routes or encrypted static blobs would still ship the private data to anonymous clients and cannot provide a genuine authorization boundary.

### Separate SaaS identity, database and object storage

Deferred. It adds vendors, data processors, integration failure modes and cost disproportionate to a single-candidate, low-volume portal. SQLite is sufficient while the service remains single-node.

### Replace Nginx with the application service

Rejected. It would unnecessarily disturb the proven static gateway, public header/routing contract and pull-only deployment.

## Consequences

Positive:

- public agent discovery becomes transparent, consistent and evidence-led;
- exact terms, messages and résumé data gain a real authentication boundary;
- publication cannot drift across machine surfaces;
- recruiter access and résumé decisions are auditable and revocable; and
- the existing public-site performance and production-source boundary remain intact.

Costs and risks:

- operations now own a database, encryption keyring, certificate, mailbox scope, backups, migrations and retention;
- availability remains single-node and depends on SQLite volume integrity;
- certificate/key rotation requires deliberate overlap and restore testing;
- Nginx resolves its portal upstream on gateway start, so both services must be recreated together; and
- GHCR does not provide a cross-package atomic tag transaction, so the initial release is pinned to immutable tags while the `latest` pair is deliberately initialized; and
- magic-link deliverability and domain classification require monitoring and administrator judgement.

## Guardrails

- Begin with `RECRUITER_PORTAL_ENABLED=false` and enable only after full external acceptance.
- Never commit exact private terms, credentials, `.env`, secret files or private infrastructure detail.
- Test anonymous leakage across responses, assets, images, layers and logs.
- Use expand/contract database changes so the previous matching image pair can roll back.
- Disable discovery before image rollback; pin both images to the same prior commit.
- Revisit SQLite if multi-node writes, materially higher volume or formal availability requirements emerge.
