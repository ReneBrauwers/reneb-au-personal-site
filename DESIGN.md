# Design direction

## Design thesis

The page should feel like René's best executive decision paper translated into a personal website: structured, calm, clear about relationships and confident enough not to over-decorate.

The visual language combines:

- executive restraint;
- architectural structure;
- hands-on credibility;
- a subtle sense of movement from intent to execution; and
- enough warmth to feel like a person, not a corporate function.

The result should feel premium and specific to René. Avoid common personal-site tropes such as giant floating headshots, résumé timelines, skill bars, neon technology clouds and walls of badges.

## Recruiter portal extension

The recruiter preview and protected portal extend the same editorial system rather than introducing a SaaS-dashboard aesthetic.

- Public recruiter pages use the navy, paper and restrained warm-accent palette with evidence cards and explicit fit/non-fit groupings.
- Protected pages show a clear private-information banner and use plain forms, definition lists and tables.
- Body copy is at least 18px by default; every primary interaction has a 44px target.
- The circular `RB` editorial mark must not resemble a shield, badge or tombstone.
- Avoid charts, score meters or “AI match percentage” claims. Evidence and mandate overlap carry the argument.
- Preserve `overflow-x: clip` and `overscroll-behavior-x: none`, and verify forced horizontal movement in Chromium and WebKit touch contexts.
- Content Studio uses a minimal self-hosted Quill toolbar for headings, emphasis, lists and safe links. Typed fields and repeaters must look like ordinary labelled forms, not raw JSON or a page-builder canvas.
- Draft previews use the real semantic content hierarchy and remain usable at 390px and desktop widths. Diffs, history, provider settings and generated machine previews may scroll inside their own bounded container but never widen the page root.
- AI authoring clearly distinguishes provider/model, selected public/private context, retention disclosure, proposal diff and human-only publication. Egress-disabled and untested-provider states must be obvious.

## Core visual idea

Use three connected paths or nodes to represent:

```text
Business ↔ Architecture ↔ Engineering
```

The motif may appear:

- as a restrained line system behind or beside the hero;
- as the transition between the hero and value section;
- as a small footer mark; and
- in the social sharing card.

It must not look like a network topology, cloud architecture diagram or data-flow animation. The relationship should remain legible without text embedded in an image.

If lines animate, use a single slow reveal or directional highlight after load. Do not run a perpetual high-energy animation. Respect `prefers-reduced-motion`.

## Art direction

Descriptors:

- editorial
- architectural
- intelligent
- measured
- grounded
- modern but not trendy
- approachable
- quietly opinionated

Avoid:

- generic Azure blue;
- purple AI gradients;
- glassmorphism;
- glowing nodes;
- stock-office imagery;
- Sydney skyline photography;
- literal bridges;
- puzzle pieces;
- handshakes;
- chess pieces;
- cloud-provider icons;
- circuit-board textures;
- fake whiteboards or dashboards; and
- overly formal boardroom portrait styling.

## Colour system

Use these as the starting tokens. Minor contrast-driven adjustment is allowed.

| Token | Hex | Use |
| --- | --- | --- |
| `--ink` | `#0A1624` | Main text and deep backgrounds |
| `--navy` | `#102A43` | Brand surface and primary action |
| `--slate` | `#526779` | Secondary text |
| `--line` | `#CBD6DE` | Dividers and connected-path motif |
| `--mist` | `#EAF0F4` | Soft section background |
| `--paper` | `#F7F9FA` | Main page background |
| `--white` | `#FFFFFF` | Inverse text and cards |
| `--warm` | `#C86632` | Restrained accent |
| `--warm-soft` | `#F0D8CA` | Accent wash or selected path |
| `--focus` | `#0A6FC2` | Accessible focus ring |

Rules:

- The warm accent should occupy less than roughly 10% of the page.
- Do not place small white text on `--warm` unless contrast is validated.
- Primary buttons should normally use navy, not warm.
- Gradients are optional and must be subtle tonal shifts within navy/mist, not multi-colour effects.
- All text combinations must meet WCAG 2.2 AA.

## Typography

Preferred no-request font stack:

```css
font-family:
  Inter,
  Aptos,
  "Segoe UI",
  Roboto,
  Helvetica,
  Arial,
  sans-serif;
```

Use a self-hosted variable font only if it is supplied with an appropriate licence. Do not depend on Google Fonts or another remote font service.

Scale guidance:

| Role | Desktop | Mobile | Notes |
| --- | --- | --- | --- |
| Hero H1 | `clamp(3.25rem, 7vw, 6.75rem)` | same clamp | Tight leading, balanced measure |
| Section H2 | `clamp(2rem, 4vw, 3.75rem)` | same clamp | Maximum 18–22 characters per line where practical |
| Card H3 | `1.2–1.4rem` | `1.15–1.3rem` | Strong but not oversized |
| Body large | `1.15–1.3rem` | `1.05–1.15rem` | 1.55–1.7 line-height |
| Body | `1rem` | `1rem` | Comfortable line-height |
| Eyebrow | `0.75–0.85rem` | same | Uppercase optional; generous tracking |

Use `text-wrap: balance` for headings where supported, with graceful fallback. Keep body measure around 60–72 characters.

## Page composition

### 1. Header

- Maximum content width aligned with the page grid
- Small `RB` monogram at left
- One LinkedIn CTA at right
- Approximately 64–80px high
- No hamburger menu
- May become sticky, but only if it does not consume excessive mobile space

### 2. Hero

Desktop:

- Minimum height around `min(820px, 88svh)` while preserving content visibility
- Asymmetric two-column composition
- Copy occupies approximately 60–65%
- Visual/headshot occupies approximately 35–40%
- Primary CTA and secondary anchor appear together
- Connected-path motif sits behind or around the visual, never behind dense body text

Mobile:

- Copy first
- Headshot or monogram visual second
- CTA remains obvious
- Avoid a full-viewport hero that hides the next section

### 3. Alignment motif

Present Business, Architecture and Engineering as peers in one decision system.

Good treatments:

- three labelled nodes on an adaptable curved path;
- a three-column band with one continuous line; or
- a vertical sequence on narrow screens.

Bad treatments:

- Architecture above Business and Engineering;
- a Venn diagram;
- arrows suggesting one-way control from Architecture;
- a complex SVG with tiny labels.

### 4. Value section

Use three cards or editorial columns. The section needs strong hierarchy but should not resemble SaaS feature pricing.

On desktop, three columns are appropriate. On mobile, stack with clear spacing. Use subtle numbering (`01`, `02`, `03`) instead of generic icons if that feels stronger.

### 5. Perspective section

Use a darker navy field or a strong tonal change. Allow the three principles to read as a sequence. The optional pull quote may become the dominant typographic element.

### 6. About section

Use a generous text layout and, if supplied, the approved headshot. Avoid biography cards or résumé timeline styling.

### 7. Closing CTA and footer

The CTA should feel open and personal. Reuse the connected-path motif in a simplified form to visually complete the page.

## Headshot handling

An approved professional headshot is preferred. It should:

- preserve René's natural appearance;
- feel approachable, credible and executive;
- show a head-and-shoulders composition;
- use neutral or soft contextual background;
- avoid heavy retouching;
- have a real natural smile rather than a synthetic corporate expression; and
- be exported as responsive AVIF/WebP with a JPEG fallback if required.

Do not:

- scrape the current LinkedIn or X profile photo;
- generate a new face without identity references and explicit instruction;
- reshape facial features;
- over-smooth skin;
- add vendor branding; or
- use a formal suit-and-tie image if a more natural approved headshot exists.

When no approved headshot is supplied, use a designed monogram composition:

- large `RB` letterform;
- one connected warm path crossing or framing the initials;
- navy/mist palette;
- abstract rather than logo-heavy;
- accessible text remains outside the graphic.

## Social card

Create a 1200 × 630 social card that includes:

- René Brauwers
- “Better technology decisions. Stronger business outcomes.”
- the Business ↔ Architecture ↔ Engineering motif
- `reneb.au`

Do not include employer or technology logos. Use the approved headshot only if its crop remains natural.

## Interaction

Allowed:

- subtle header transition on scroll;
- section reveal with opacity/translation under 16px;
- one-time path drawing;
- hover and focus feedback on links;
- smooth anchor scrolling when motion is allowed.

Disallowed:

- parallax that moves content away from the pointer;
- continuous particle systems;
- cursor-follow effects;
- text scrambling;
- autoplay video;
- magnetic buttons;
- horizontal scrolling;
- animations that delay reading; and
- motion without a reduced-motion fallback.

All meaningful content must remain available with CSS animation disabled and JavaScript off.

## Responsive grid

- Page max width: `1180–1280px`
- Desktop gutters: `32–48px`
- Tablet gutters: `24–32px`
- Mobile gutters: `18–24px`
- Section spacing: use fluid spacing with `clamp()`
- Test at 320, 390, 768, 1024, 1440 and 1920px widths

Avoid fixed heights for content sections. Use logical properties where practical.

## Component details

Buttons:

- minimum target size 44 × 44px;
- visible hover and focus states;
- modest radius, approximately 8–14px;
- no pill shape unless used consistently and sparingly;
- primary button solid navy;
- secondary action as an underlined or bordered text link.

Cards:

- avoid large drop shadows;
- use borders, tonal surfaces or whitespace;
- maintain equal visual weight;
- do not force equal height if it harms mobile reading.

Links:

- visible affordance beyond colour in body content;
- meaningful labels;
- no raw URLs in visible copy.

## Favicon and monogram

Use the initials `RB`, not `R` alone. The favicon must remain legible at 16px. A simple navy square or circle with white initials and one warm path is sufficient.

Do not create an elaborate standalone brand mark that competes with René's name.
