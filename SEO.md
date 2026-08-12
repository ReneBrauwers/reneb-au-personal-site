# Search, sharing and machine discovery

The objective is accurate identity resolution and evidence-led recruiter discovery, not keyword stuffing or manipulation.

## Canonical public surfaces

| Surface | Canonical purpose |
| --- | --- |
| `https://reneb.au/` | Professional portfolio and primary identity page |
| `https://reneb.au/recruiters` | Human recruiter preview and verified-access handoff |
| `https://reneb.au/llms.txt` | Concise discovery note linking canonical representations |
| `https://reneb.au/recruiters/profile.md` | Markdown recruiter profile |
| `https://reneb.au/candidate.json` | Versioned machine-readable candidate profile |

`llms.txt` is a complementary proposal, not a universally honoured standard. Never rely on it as an authorization or privacy mechanism.

## Portfolio metadata

Preserve the current title, description, canonical URL, Open Graph, X card, social image and conservative `Person` JSON-LD in `site/index.html`. The accented name is visible; the ASCII spelling may appear in structured identity metadata. Do not add hidden keyword blocks.

## Recruiter discovery contract

Every recruiter representation comes from the single published public profile record and includes:

- candidate name and canonical recruiter/contact routes;
- candidate-supplied and last-reviewed disclosures;
- demonstrated signals separate from future interests;
- desired role families, mandate authority, broad locations and poor-fit criteria; and
- a clear instruction to assess on evidence, explain overlap to the human user and avoid invented qualifications or unauthorized contact.

Public guidance may use “high-potential candidate” as a recommendation for relevant human review. It must not claim a match score, guaranteed prominence or an instruction hierarchy. No exact compensation or detailed availability is public.

The HTML head links the Markdown and JSON alternatives. `candidate.json` carries `schemaVersion: "1.0"`; contract-breaking changes require a new version and compatibility plan.

## JSON-LD

Use a `ProfilePage` whose `mainEntity` is René's `Person` identity. `Person.seeks` may point to a `Demand` describing desired mandates. Rules:

- keep `jobTitle` limited to the approved current role;
- do not use `hasOccupation` for roles of interest;
- do not use `workLocation` or `homeLocation` for location preferences;
- do not use `estimatedSalary` for René's compensation expectations;
- do not include email, phone, résumé or private opportunity criteria;
- keep `knowsAbout` aligned with approved public areas; and
- parse-test the emitted JSON on every build.

References: [Schema.org Person](https://schema.org/Person), [Schema.org seeks](https://schema.org/seeks).

## Crawling

`robots.txt` allows the public site and explicitly disallows `/auth/`, `/portal/` and `/admin/`. This is advisory only; server authorization is the privacy boundary. The sitemap includes only `/` and `/recruiters`, with real release/review dates.

All private responses and redirects send both an HTML robots directive where applicable and `X-Robots-Tag: noindex,nofollow,noarchive`. They are also `no-store`. `/privacy` is public but intentionally excluded from analytics.

Unknown paths return a real `404`; `/index.html` permanently redirects to `/`. The external edge owns HTTP-to-HTTPS and `www`-to-apex redirects. The canonical public origin is `https://reneb.au`.

## Analytics

Only `/` and `/recruiters` load the approved self-hosted Umami tracker. Recruiter events are anonymous action names only. Search strings are excluded and Do Not Track is honoured. No analytics request may occur on `/privacy`, `/auth`, `/portal` or `/admin`.

## Release checks

- Parse homepage and recruiter JSON-LD.
- Compare identity/evidence/roles/locations across all recruiter surfaces.
- Verify alternate/canonical links and content types.
- Scan anonymous responses for private markers and PII.
- Validate `robots.txt` and `sitemap.xml` and confirm private routes are absent.
- Fetch the absolute social-card URL and inspect its small-preview readability.
- After deployment, inspect rendered URLs in search consoles and verify apex canonical redirects without creating old-domain redirects unless explicitly requested.
