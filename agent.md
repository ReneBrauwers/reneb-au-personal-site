# Authoritative agent instructions

## Mission

Create a polished, professional, one-page static personal-brand website for René Brauwers at `reneb.au`.

The page must work as the useful next step after meeting René or viewing his LinkedIn profile. Within roughly one minute, a visitor should understand:

- who René is;
- the business problems he helps leaders and teams work through;
- how he approaches technology decisions;
- why his perspective is credible; and
- how to connect with him.

The site must express executive judgment and hands-on technical credibility at the same time. It must not read like a résumé, vendor profile, architecture framework brochure or generic AI-generated leadership page.

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
3. Build the complete site in `site/`.
4. Use the approved content and resolved decisions. Do not pause for optional preferences.
5. If no headshot is available, implement the monogram fallback and continue.
6. Validate HTML, responsive layout, keyboard access, reduced-motion behaviour, metadata and external links.
7. Render and visually inspect at the viewport sizes listed in `QA.md`.
8. Fix all material issues before handoff.
9. Provide a short handoff stating what was built, how it was validated and which optional items still require René's input.

## Output structure

Use this implementation structure unless an existing project reasonably requires a different one:

```text
site/
├── index.html
├── styles.css
├── script.js              # only if progressive enhancement needs it
├── favicon.svg
├── social-card.svg        # or an optimised social-card.webp/png
├── robots.txt
├── sitemap.xml
└── assets/
    ├── rene-headshot.*    # only when an approved source image exists
    └── fonts/             # only if fonts are self-hosted
```

Do not add a framework, package manager or build step for a page that does not need one. If the surrounding repository already uses a framework, integrate cleanly but ensure the deployed result remains a static page.

## Implementation rules

- Use semantic HTML and progressive enhancement.
- Keep page copy approximately 450–700 words, excluding metadata and accessibility text.
- Use one `h1` and a logical heading hierarchy.
- Use real text, not text rendered into images.
- Keep the primary CTA visible without scrolling at common desktop sizes.
- Use the exact verified links from `DECISIONS.md`.
- Open external profile links in the same tab by default. If a new tab is used, disclose it accessibly and add `rel="noopener noreferrer"`.
- Load only the approved cookieless Umami tracker from `https://stats.reneb.au/script.js`, restricted to `reneb.au` with website ID `55c627ba-826f-4472-9479-f1279071488c`. Do not add any other analytics, tag managers, advertising, chat widgets, tracking pixels or cookie banners.
- Do not add a contact form. There is no approved form endpoint or public email address.
- Do not add a blog, navigation drawer, résumé download, client logo wall, skills meter, testimonial carousel, animated cursor or fake terminal.
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
- there is a graceful fallback when no headshot is available; and
- the final result feels recognisably tailored to René rather than generated from a personal-site template.
