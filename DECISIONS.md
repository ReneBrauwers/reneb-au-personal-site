# Resolved decisions

These decisions are intentional defaults. Do not ask René to re-decide them during the first build unless his latest instruction explicitly changes one.

## Identity and positioning

| Decision | Resolved value |
| --- | --- |
| Visible name | René Brauwers |
| Domain | `https://reneb.au` |
| Location | Sydney, Australia |
| Primary identity | Business-first technology advisor and enterprise architecture leader |
| Current role | Head of Enterprise Architecture at Perpetual Corporate Trust |
| Sector context | Regulated financial services |
| Core proposition | Helping Business and Engineering make better technology decisions that deliver sustainable business outcomes |
| Distinctive bridge | Business ↔ Architecture ↔ Engineering |
| Personal tagline to avoid | “Architect who aligns business and IT” — too generic |

Use the accent in **René** in all visible copy. ASCII `Rene` is acceptable only where a platform identifier, filename, URL or external profile uses it.

## Audience

Prioritise, in order:

1. People René has recently met who want a concise professional introduction
2. Senior business, product, risk, data and technology leaders
3. Engineering and architecture peers
4. Recruiters and potential future employers
5. Community contacts interested in enterprise technology, AI, architecture, integration and automation

The page is not aimed at entry-level technical audiences and should not attempt to teach Azure products.

## Page model

- The primary portfolio remains a one-page, framework-free public site
- English only
- Add a visible `/recruiters` preview plus `/llms.txt`, `/recruiters/profile.md` and `/candidate.json`
- Add an authenticated ASP.NET Core recruiter portal under `/auth`, `/portal` and `/admin`
- Publish a linkable, analytics-free collection notice at `/privacy`
- No blog, consulting price list, public contact form or public résumé
- Exact compensation, detailed availability and messages require verified recruiter access
- Résumé download requires an explicit revocable 30-day administrator grant
- No testimonials until real, approved statements exist
- No employer, client or technology logo wall

## Approved link strategy

Primary CTA:

- Label: **Connect on LinkedIn**
- URL: `https://www.linkedin.com/in/renebrauwers/`

Secondary social link:

- Label: **X**
- Handle: `@Rene_B`
- URL: `https://x.com/Rene_B`

Do not publish a public email address yet. Historical `brauwers.nl` email addresses are not automatically approved for the new site.

## Employer treatment

The current role and employer may be stated in text because they are current public profile information.

Do not:

- use the Perpetual or Perpetual Corporate Trust logo;
- imply that the site is an employer publication;
- describe confidential initiatives, clients, systems or internal responsibilities;
- publish internal stakeholder names; or
- use “we” when expressing René's personal perspective.

Include a subtle footer statement:

> Personal site. Views are my own.

## Historical credibility

The following are publicly evidenced but should remain secondary to the current value proposition:

- former Microsoft Azure MVP;
- long-running technical writing on Azure, integration and automation;
- technical reviewer of Microsoft integration books;
- community speaking and involvement in Integration Down Under;
- hands-on progression from developer and integration specialist to architecture and enterprise leadership.

Version-one default: include **one understated sentence** about prior Microsoft MVP recognition and community contribution. Do not turn these into badges or a trophy wall.

Do not state exact MVP years or award counts without fresh confirmation.

## Visual identity

- Premium editorial, not a generic executive template
- Muted navy and slate foundation
- Restrained warm terracotta/amber accent
- Abstract connected paths as the recurring motif
- Approved professional headshot when supplied
- Designed `RB` monogram fallback when no headshot exists
- No stock imagery, city skyline, cloud-provider art or literal architecture diagrams

## Behaviour and privacy

- Public portfolio and recruiter preview remain fast and server-rendered/static
- Self-hosted, cookieless Umami analytics at `stats.reneb.au` is permitted only on `/` and `/recruiters`, limited to aggregate usage and approximate country/region/city
- No analytics, session replay or tracking on `/auth`, `/portal` or `/admin`
- Essential secure, HTTP-only authentication and antiforgery cookies are permitted only for protected workflows
- No tracking pixels
- No advertising identifiers or third-party analytics
- No automatic X or LinkedIn embeds
- Public personal details remain limited to approved professional facts and recruiter fit signals
- Private recruiter and candidate information is encrypted before SQLite storage
- Authentication cookies are backed by revocable server-side sessions; persisted cookie-key material uses a certificate separate from Microsoft Graph credentials

## Future options, not part of version one

- Short “notes” or writing section under a separate path
- Downloadable vCard after contact details are explicitly approved
- Speaking or publications archive
- Dark/light theme toggle
- Dutch-language version

Do not build these speculatively.
