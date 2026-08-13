# Authoritative agent instructions

## Mission

Maintain a polished public personal-brand website and a secure recruiter-discovery portal for René Brauwers at `reneb.au`.

The page must work as the useful next step after meeting René or viewing his LinkedIn profile. Within roughly one minute, a visitor should understand:

- who René is;
- the business problems he helps leaders and teams work through;
- how he approaches technology decisions;
- why his perspective is credible; and
- how to connect with him.

The public portfolio must express executive judgment and hands-on technical credibility at the same time. The recruiter preview must provide evidence-led fit signals without manipulating agents or exposing private terms. The authenticated portal owns exact opportunity criteria, recruiter messages and approval-controlled résumé access.

## Required reading order

Before building anything, read these files in order:

1. `DECISIONS.md`
2. `CONTENT.md`
3. `DESIGN.md`
4. `REQUIREMENTS.md`
5. `PRIVACY.md`
6. `SEO.md`
7. `QA.md`
8. `RESEARCH.md`
9. `brand-data.json`

Use `RESEARCH.md` as evidence, not as page copy. Where the approved copy and historical source material differ, follow `CONTENT.md`.

## Brand interpretation

The central idea is:

> Business outcomes, architecture judgment and engineering reality belong in the same conversation.

René's differentiator is not that he knows a long list of technologies. It is that he can:

- start with the business consequence;
- make investment, risk and operating trade-offs visible;
- use architecture to improve decisions rather than create ceremony;
- connect strategy to executable engineering choices; and
- remain close enough to delivery to know when a recommendation is impractical.

His historical public work establishes a hands-on foundation in software, integration, automation, Microsoft Azure, technical writing and community contribution. His current position is at enterprise leadership altitude in regulated financial services. The page should communicate the progression from builder to trusted decision advisor without presenting a chronological career history.

## Build workflow

1. Inspect the project and confirm whether an approved headshot or existing brand assets were supplied.
2. Create a concise implementation plan.
3. Build public assets in `site/` and all server-rendered pages, content publishing and recruiter capabilities in `portal/`.
4. Use the approved content and resolved decisions. Do not pause for optional preferences.
5. If no headshot is available, implement the monogram fallback and continue.
6. Validate HTML, responsive layout, keyboard access, reduced-motion behaviour, metadata and external links.
7. Render and visually inspect at the viewport sizes listed in `QA.md`.
8. Fix all material issues before handoff.
9. Provide a short handoff stating what was built, how it was validated and which optional items still require René's input.

## Output structure

Use this implementation structure:

```text
site/
├── styles.css
├── favicon.svg
├── social-card.svg        # or an optimised social-card.webp/png
└── assets/
    ├── rene-headshot.*    # only when an approved source image exists
    └── fonts/             # only if fonts are self-hosted
portal/                    # ASP.NET Core Razor Pages, SQLite and protected workflows
portal.tests/              # behaviour and security tests
nginx/                     # sole public gateway and route contract
deploy/                    # pull-only production Compose and runbook
```

Do not add a frontend framework or convert the public portfolio to a single-page application. Preserve Nginx as the only exposed service and the portal as the only approved application runtime.

## Implementation rules

- Use semantic HTML and progressive enhancement.
- Keep page copy approximately 450–700 words, excluding metadata and accessibility text.
- Use one `h1` and a logical heading hierarchy.
- Use real text, not text rendered into images.
- Keep the primary CTA visible without scrolling at common desktop sizes.
- Use the exact verified links from `DECISIONS.md`.
- Open external profile links in the same tab by default. If a new tab is used, disclose it accessibly and add `rel="noopener noreferrer"`.
- Load only the single administrator-published cookieless Umami tracker on `/` and `/recruiters`. Its typed settings default to `https://stats.reneb.au/script.js`, `reneb.au` and website ID `55c627ba-826f-4472-9479-f1279071488c`; never replace the governed fields with arbitrary script/HTML injection. Do not add any other analytics, tag managers, advertising, chat widgets, tracking pixels or cookie banners.
- Treat Content Studio as the only editorial source. Rich text is validated Quill Delta; layouts, routes, security text and machine schemas remain code-governed. AI may propose drafts only and never publishes.
- Do not add a public contact form or public email address. The authenticated recruiter message box is approved and stores encrypted content.
- Do not add a public résumé download. The authenticated, administrator-approved, revocable 30-day résumé grant is the only approved download path.
- Do not add a blog, navigation drawer, client logo wall, skills meter, testimonial carousel, animated cursor or fake terminal.
- Do not use vendor logos or turn the page into a product stack.
- Do not use fabricated statistics such as “25+ years”, “100+ projects” or “millions saved”.
- Do not use copied LinkedIn or X imagery as a substitute for an approved asset.
- Avoid visual effects that resemble a generic SaaS landing page: excessive gradients, glowing blobs, glass cards, floating dashboards and decorative grids.

## Tone rules

Copy must be:

- direct and concise;
- business-first;
- assured without self-importance;
- specific about consequences and trade-offs;
- readable by business and engineering audiences;
- written in Australian English; and
- free of inflated phrases such as “visionary”, “thought leader”, “world-class”, “passionate innovator”, “digital transformation guru” or “at the forefront”.

Architecture is a means, not the hero. Technology names may appear in metadata or historical research but should not dominate the visible page.

## Fact discipline

Claims have three states:

- **Approved current:** may be used as written in `CONTENT.md`.
- **Verified historical:** may be used only where `CONTENT.md` explicitly includes or permits it.
- **Unconfirmed or private:** omit completely.

If a claim cannot be supported by this pack or a user-supplied source, remove it. Never “improve” copy by adding plausible achievements.

## Design discipline

Follow `DESIGN.md` closely. The desired experience is editorial and architectural: disciplined typography, visible relationships, deliberate whitespace and one restrained warm accent.

The recurring visual motif is a set of connected paths representing:

```text
Business ↔ Architecture ↔ Engineering
```

This motif is conceptual, not a literal enterprise architecture diagram. It should support the page, not become an interactive toy.

## Definition of done

The work is complete only when:

- every mandatory requirement in `REQUIREMENTS.md` is met;
- the privacy guardrails have been checked;
- the site passes the full `QA.md` checklist;
- the page works with JavaScript disabled;
- no placeholder copy, dead links or missing assets remain;
- public recruiter HTML, JSON-LD, Markdown, JSON and `llms.txt` agree with the published profile record;
- homepage, footer, privacy, opportunity and discovery copy are editable through versioned drafts with diff, preview, TOTP publish and rollback;
- Umami settings are editable only through typed validated fields and private/admin pages remain analytics-free;
- configured OpenRouter/xAI credentials remain separately encrypted, cost-limited and disabled until authenticated discovery plus structured-output testing succeeds;
- private values never appear in anonymous responses, source, static assets, image layers, analytics or logs;
- authentication, revocable sessions, TOTP, account approval, messaging, résumé validation/grants, retention, backup and restore checks pass;
- there is a graceful fallback when no headshot is available; and
- the final result feels recognisably tailored to René rather than generated from a personal-site template.
