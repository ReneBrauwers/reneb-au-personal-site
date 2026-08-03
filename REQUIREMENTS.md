# Website requirements

## Scope

Build a single static landing page for `reneb.au`. The finished page must be deployable to any ordinary static host without a server-side runtime.

Version one includes:

- responsive one-page layout;
- approved copy from `CONTENT.md`;
- LinkedIn and X links;
- approved headshot or monogram fallback;
- social sharing image;
- favicon;
- SEO metadata and structured data;
- accessible motion and interaction;
- approved self-hosted, cookieless Umami analytics;
- `robots.txt`; and
- `sitemap.xml`.

Version one excludes:

- CMS;
- blog;
- contact form;
- authentication;
- cookie consent;
- live social feeds;
- third-party embeds;
- application API calls or server-side runtime;
- downloadable CV;
- newsletter;
- calendar booking;
- multilingual content; and
- employer/client logos.

## Technical baseline

- HTML5, CSS and optional lightweight JavaScript
- No framework required
- No external runtime dependencies
- No JavaScript dependency for reading or navigation
- All site-owned asset paths must work from the domain root
- UTF-8 throughout so `René` renders correctly
- Valid canonical URL: `https://reneb.au/`
- Add a meaningful `<noscript>` only if enhancement truly needs it; do not show a warning merely because JavaScript is off

If a repository already has a build system, the agent may use it. The production output must still be a static page and must not ship unnecessary client JavaScript.

## Semantic structure

Minimum:

```html
<header>
<main>
  <section aria-labelledby="..."> <!-- hero -->
  <section aria-labelledby="..."> <!-- alignment -->
  <section aria-labelledby="..."> <!-- value -->
  <section aria-labelledby="..."> <!-- perspective -->
  <section aria-labelledby="..."> <!-- about -->
  <section aria-labelledby="..."> <!-- closing CTA -->
<footer>
```

Requirements:

- one `h1`;
- logical `h2`/`h3` sequence;
- skip link as the first focusable control;
- real links or buttons according to behaviour;
- no clickable `div` elements;
- decorative SVGs hidden from assistive technology;
- meaningful image alternative text;
- landmarks must not be duplicated without labels.

## Responsive behaviour

The page must remain usable without horizontal scrolling from 320px through 2560px.

Explicitly test:

- 320 × 568
- 390 × 844
- 768 × 1024
- 1024 × 768
- 1440 × 900
- 1920 × 1080

Long headings must wrap intentionally. CTA labels must not truncate. The alignment motif must switch to a vertical or compact layout when three horizontal labels no longer fit.

## Accessibility

Target WCAG 2.2 AA.

Mandatory:

- contrast compliant in normal, hover, visited and focus states;
- full keyboard operation;
- obvious focus indicator;
- 44px minimum target size for primary interactions;
- correct `lang="en-AU"`;
- reduced-motion support;
- zoom to 200% without content loss;
- text reflow at 400% where applicable;
- no information conveyed by colour alone;
- no autoplaying or endlessly distracting motion;
- accessible name for monogram/home link;
- accessible description for any link that intentionally opens a new tab;
- meaningful heading and link text;
- SVG title/description only where the SVG carries information;
- hidden decorative visual paths.

## Performance

Targets under a production build and ordinary broadband/mobile throttling:

- Lighthouse Performance: 95+
- Lighthouse Accessibility: 100 preferred, 95 minimum with documented reason
- Lighthouse Best Practices: 95+
- Lighthouse SEO: 100 preferred
- LCP under 2.5 seconds
- CLS under 0.1
- INP under 200ms
- initial page transfer ideally below 500KB without the high-resolution social card

Implementation guidance:

- responsive image dimensions to prevent layout shift;
- AVIF/WebP for photographic assets;
- lazy-load only content below the fold;
- do not lazy-load the LCP image;
- inline only small critical SVG/CSS where it materially helps;
- no remote font dependency;
- no unused icon library;
- no large animation library;
- cache-bust versioned assets only when the chosen host needs it.

## Privacy and security

- Do not set cookies or local storage
- Only the approved Umami tracker at `https://stats.reneb.au/script.js`, using website ID `55c627ba-826f-4472-9479-f1279071488c` and `data-domains="reneb.au"`
- No pixels, advertising identifiers, tag managers or other analytics providers
- Limit analytics to aggregate usage and approximate country/region/city resolution
- Include a concise visible analytics notice
- No third-party embeds
- No live social widgets
- Add `referrerpolicy="strict-origin-when-cross-origin"` where appropriate
- Use `rel="noopener noreferrer"` for new-tab external links
- Avoid inline scripts where a strict CSP is expected; use a local script file
- Do not expose email addresses in source code
- Do not include source maps in the deployed production output unless intentional
- Do not include comments containing private background information

When the hosting platform is chosen, configure headers where supported:

```text
Content-Security-Policy
Referrer-Policy: strict-origin-when-cross-origin
X-Content-Type-Options: nosniff
Permissions-Policy: camera=(), microphone=(), geolocation=()
Strict-Transport-Security
```

Do not add provider-specific configuration until the target host is known. If adding a CSP, test every asset and do not weaken it with `unsafe-eval`.

## Links

Required:

- LinkedIn: `https://www.linkedin.com/in/renebrauwers/`
- X: `https://x.com/Rene_B`
- Canonical: `https://reneb.au/`

No old blog, historical email, employer site or archived source needs to appear on the public page.

## Metadata and files

Implement:

- `<title>`;
- meta description;
- canonical link;
- Open Graph fields;
- X/Twitter card fields;
- `theme-color`;
- favicon;
- JSON-LD `Person`;
- `robots.txt`;
- `sitemap.xml`; and
- 1200 × 630 social card.

See `SEO.md` for exact guidance.

## Deployment readiness

The completed site must:

- render correctly when served by a simple local HTTP server;
- contain no absolute local filesystem paths;
- contain no development-only endpoints;
- contain no broken source-map references;
- use HTTPS URLs for all external resources;
- not assume a trailing path other than `/`;
- include a short deployment note for the selected static host; and
- be safe to preview before the domain is connected.

Recommended generic local check:

```bash
python3 -m http.server 8080 --directory site
```

The agent may use another preview server when appropriate.

## Browser support

Support current and previous major versions of:

- Chrome
- Edge
- Firefox
- Safari
- Mobile Safari
- Chrome for Android

Use progressive enhancement for newer CSS such as `text-wrap: balance`. Do not require experimental APIs.

## Definition of acceptable content drift

Allowed:

- punctuation and line-break adjustments;
- slight copy shortening at narrow widths;
- replacing “I’m” with “I am” where style requires;
- choosing one approved alternative from `CONTENT.md`.

Not allowed without user approval:

- changing the core positioning;
- adding services or job-seeking language;
- adding claims or metrics;
- naming clients or internal initiatives;
- changing current employer;
- publishing an email address; or
- converting the page into a technical portfolio.
