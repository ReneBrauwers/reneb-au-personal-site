# Privacy and publication boundaries

This file is mandatory. The website is public, searchable and durable. Public availability elsewhere does not automatically make a fact appropriate for this page.

## Publication principle

Publish the minimum personal information required to establish professional identity, relevance and credibility.

The page should make René easy to understand and easy to contact professionally. It should not make him easy to profile personally.

## Approved public facts

The version-one site may state:

- name: René Brauwers;
- location: Sydney, Australia;
- current role: Head of Enterprise Architecture;
- current employer: Perpetual Corporate Trust;
- current sector context: regulated financial services;
- work across Business, Engineering, Product, Data, Security and Risk;
- business-first technology advisor and enterprise architecture leader positioning;
- professional roots in software, integration, automation and cloud;
- former Microsoft Azure MVP recognition;
- historical technical writing, book reviewing and community contribution;
- LinkedIn profile URL; and
- X handle and URL.

These facts still need to be expressed with the restraint defined in `CONTENT.md`.

## Do not publish

Never include:

- date of birth or age;
- citizenship or nationality matters;
- marital or family details;
- home suburb, street or precise residence;
- phone numbers;
- personal or historical email addresses;
- compensation, savings, mortgage or other finances;
- health, medication, fitness or dietary information;
- travel plans or availability;
- private hobbies that René has not chosen to make part of the brand;
- internal stakeholder names;
- internal employer projects, systems, governance discussions or incidents;
- client-confidential project detail;
- contract values, budgets or performance data not already approved and public;
- private repositories, server names, IP addresses or infrastructure details;
- social account activity unrelated to the professional purpose;
- copied contact data found in old books, cached pages or domain records; or
- metadata in images that reveals location or device details.

Do not put excluded facts into HTML comments, JSON-LD, source maps, image metadata, filenames or deployment notes.

## Employer boundary

This is René's personal site.

- Do not imply employer sponsorship or endorsement.
- Do not use employer visual identity.
- Do not reproduce internal job descriptions.
- Do not describe current work beyond the approved public summary.
- Do not make claims on behalf of Perpetual Corporate Trust.
- Include “Personal site. Views are my own.”

## Historical sources

Old websites and archived pages contain stale roles, contact details and personal references. Use them only to understand professional themes and public contribution.

Specifically:

- do not republish the old `brauwers.nl` email addresses;
- do not state the archived Servian role as current;
- do not reuse names of family, friends or former colleagues from book acknowledgements;
- do not link to compromised, parked or stale domains from the public site;
- do not present old certification IDs; and
- do not assume an old social handle is still controlled by René.

## Social imagery

Do not scrape LinkedIn or X profile images.

Use:

1. an image René directly supplies and approves;
2. a previously approved local brand asset; or
3. the monogram fallback in `DESIGN.md`.

Strip EXIF and other unnecessary metadata from final published images.

## Contact strategy

Until René approves a dedicated public email:

- use LinkedIn as the primary professional contact path;
- use X as an optional secondary connection;
- do not obfuscate and publish an old email;
- do not build a form that sends to an undisclosed endpoint; and
- do not add a scheduling platform.

## Analytics

Approved implementation: self-hosted Umami at `https://stats.reneb.au`.

The implementation must remain:

- privacy-preserving;
- cookieless and free of browser identifiers stored by the site;
- documented;
- limited to aggregate page performance and usage;
- free of cross-site advertising identifiers; and
- accompanied by a concise visible privacy notice.

Cloudflare supplies approximate country, region and city headers to Umami. Umami uses the requesting IP address transiently to derive a rotating session identifier and location, but the raw IP address is not stored in the PostgreSQL analytics schema. Do not enable fingerprinting, advertising integrations, session replay or custom event payloads containing personal information.

The only approved production tracker is `https://stats.reneb.au/script.js`, restricted to `reneb.au` with website ID `55c627ba-826f-4472-9479-f1279071488c`. Any other analytics host, identifier or collection purpose requires a fresh privacy review.

## Research hygiene

Do not copy search-engine snippets into production. Search results establish leads, not final claims. Use the approved content deck and source ledger.

If a future agent performs fresh research, it must:

- distinguish René Brauwers from people with similar names;
- prefer first-party or primary sources;
- record the retrieval date;
- classify the result as current, historical or uncertain;
- omit private data irrelevant to the page; and
- request confirmation before publishing a new substantive claim.

## Final privacy check

Before release, search the full production output for:

- email-like strings;
- phone-number patterns;
- street/suburb references;
- internal employer terminology;
- old social handles;
- EXIF metadata;
- source-map files;
- private comments; and
- unapproved or placeholder analytics IDs.

Any unexpected match must be resolved before deployment.
