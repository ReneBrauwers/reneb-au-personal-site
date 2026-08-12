# Privacy and publication boundaries

This file is mandatory. Authentication is the only privacy boundary: anything returned to an anonymous request must be treated as public, searchable and durable.

## Public candidate information

The public portfolio and recruiter profile may publish only the approved facts in `CONTENT.md`: René's professional identity, broad Australian location preferences, current professional context, demonstrated signals, areas of interest, desired mandate characteristics and poor-fit guidance.

Public content must:

- be candidate-supplied, factual and evidence-led;
- distinguish demonstrated experience from interests and desired future mandates;
- carry a last-reviewed date;
- encourage human review without promising an agent ranking;
- avoid hidden text, cloaking, white-on-white copy and prompt-injection language; and
- be generated from the same published record across HTML, JSON-LD, Markdown, JSON and `llms.txt`.

Never publish exact compensation, detailed availability, résumé content, contact details, recruiter identity, message content, authentication material or admin configuration. This prohibition includes HTML comments, scripts, JSON-LD, client JavaScript, logs, source maps, filenames, image metadata, container layers and analytics events.

Also exclude date of birth, citizenship, family details, precise residence, health or finances unrelated to opportunity criteria, private employer/client information, private infrastructure and copied historical contact information.

## Private portal collection

Registration collects name, verified email, organisation, title, organisation or LinkedIn URL, country, sourcing purpose, privacy acknowledgement and an optional phone number. Authenticated use also creates access status, messages, résumé requests/grants and security audit metadata.

Collect this information only to:

- establish whether the requester is a genuine recruiter or hiring representative;
- decide and administer access to private candidate information;
- discuss a possible role or mandate;
- protect the portal from abuse; and
- maintain proportionate security and audit evidence.

The visible `/privacy` notice must explain collection, purpose, encryption, mail processing, retention, deletion and choice in plain language. Registration must link to it and require an explicit acknowledgement.

## Storage and access controls

- Encrypt private-profile fields, recruiter contact values, messages, résumé bytes, TOTP secrets and mail payloads at rest with a host-mounted versioned key; use a separate stable HMAC key for deterministic lookups.
- Store only hashes of one-time magic-link tokens and manual codes.
- Keep the SQLite database, backups and key files outside the web root.
- Require an active authenticated session for all private access and a separate time-limited grant for résumé downloads.
- Require TOTP step-up for administrator access and a confirmation no older than five minutes for sensitive mutations.
- Determine administrators from the current host-owned `ADMIN_EMAILS` value; a database record alone never grants admin rights.
- Redact logs and keep notification email free of private-profile values and message bodies.

## Retention and deletion

- At 150 days of inactivity, queue a warning without embedding private data.
- At 180 days, remove recruiter contact/content, sessions, grants and messages.
- Recruiter self-service deletion removes content and authentication secrets immediately. Administrator deprovisioning is a separate host-allowlist-controlled process so an administrative session cannot report or perform only a partial deletion.
- Account deletion also removes queued, sent and development-captured mail rows addressed to that account. Other mail outbox content expires after 30 days.
- Retain anonymized account metadata and metadata-only audit events for no more than 12 months, then hard-delete both.
- Résumé grants expire after 30 days and remain revocable.
- Keep only the current and one previous résumé version; securely expire older encrypted versions in accordance with the backup policy.

## Analytics

The homepage and `/recruiters` may load the approved self-hosted Umami tracker from `https://stats.reneb.au/script.js` using website ID `55c627ba-826f-4472-9479-f1279071488c`, `data-domains="reneb.au"`, `data-exclude-search="true"` and `data-do-not-track="true"`.

Only anonymous events such as `recruiter-preview-open` and `recruiter-access-start` are permitted. Never attach contact, compensation, résumé, message, organisation, sourcing-purpose or authentication data. Do not load Umami anywhere under `/auth`, `/portal`, `/admin` or on `/privacy`.

## Mail and credentials

Microsoft Graph uses certificate-based application authentication and a dedicated sender mailbox. Exchange Online Application RBAC must restrict the app to that mailbox. Certificates, private keys and the portal field-encryption keyring are host-mounted files outside source control and `.env`.

## Employer, imagery and historical sources

This remains René's personal site. Do not imply employer sponsorship, use employer identity, disclose internal work or make employer claims. Include “Personal site. Views are my own.” Do not scrape profile images; use only user-supplied approved assets or the monogram fallback and strip metadata.

Historical sources may establish leads but do not authorize republication. Do not republish old email addresses, stale jobs, certification identifiers, family/colleague names or old domains without explicit approval and current primary-source verification.

## Release privacy gate

Before release, scan every anonymous response, static asset, sitemap, source file used in either build context, image layer and captured runtime log for known compensation/availability markers, PII patterns, résumé signatures and message fixtures. Any unexplained match blocks release. Then exercise anonymous authorization failures and confirm private pages are non-cacheable, non-indexable and free of analytics requests.
