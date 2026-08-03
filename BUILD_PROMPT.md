# Master build prompt

Use the following prompt with a capable coding or website-building agent. Attach the complete creation pack.

---

Create the production-ready version-one website for `https://reneb.au/` using the attached website creation pack as the source of truth.

Start by reading `AGENTS.md` and then every file in the order specified by `agent.md`. Do not begin implementation before you understand the resolved brand, content, privacy and QA decisions.

Build a polished one-page static personal-brand site for René Brauwers. It should work as the next step after someone meets him or views his LinkedIn profile. The page must communicate that René is a business-first technology advisor and enterprise architecture leader who helps Business and Engineering make better technology decisions. His differentiator is the combination of executive decision judgment and hands-on builder credibility.

Use the approved copy in `CONTENT.md`, the visual system in `DESIGN.md`, the technical contract in `REQUIREMENTS.md`, and every publication boundary in `PRIVACY.md`.

Implementation expectations:

- Build in a new `site/` directory.
- Prefer plain semantic HTML, CSS and only minimal progressive-enhancement JavaScript.
- Do not add a framework or build pipeline unless the existing repository requires one.
- Make the page fully readable with JavaScript disabled.
- Use the Business ↔ Architecture ↔ Engineering connected-path motif with restraint.
- Use the navy/slate/warm palette from the pack.
- If an approved headshot is supplied locally, optimise and use it according to `DESIGN.md`.
- If no approved headshot exists, create the specified `RB` monogram fallback and continue without asking.
- Do not scrape LinkedIn or X imagery.
- Do not add analytics, cookies, forms, third-party embeds, employer logos, testimonials, fake metrics, a résumé timeline or a technology logo wall.
- Do not publish any email address.
- Implement all SEO, social-card, favicon, robots, sitemap and JSON-LD requirements.
- Meet WCAG 2.2 AA and the performance targets in the pack.

Before handing off:

1. Run relevant HTML, accessibility, link and performance checks.
2. Render and visually inspect the page at every viewport listed in `QA.md`.
3. Test keyboard-only, 200% zoom, reduced motion and JavaScript-disabled behaviour.
4. Fix all material visual or functional defects.
5. Confirm that no private or unapproved information appears anywhere in the production output.
6. Provide desktop and mobile screenshots or equivalent visual proof.

Deliver:

- complete deployable static output;
- source files;
- social card and favicon;
- a concise validation report;
- deployment instructions for the selected static host; and
- only a short list of genuinely optional future inputs, such as an approved headshot or public email.

Do not stop after producing a mock-up. Complete and validate the actual site.

---
