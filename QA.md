# Quality assurance and definition of done

The building agent owns validation. Do not hand off a page that has only been code-reviewed.

## 1. Content and fact check

- [ ] Visible name is spelled `René Brauwers`.
- [ ] Current role is `Head of Enterprise Architecture`.
- [ ] Employer is `Perpetual Corporate Trust`.
- [ ] Location is no more specific than `Sydney, Australia`.
- [ ] Core positioning is business-first, not a technology catalogue.
- [ ] The page describes Business and Engineering as partners.
- [ ] Architecture is presented as a decision tool, not a governance gate.
- [ ] The historical MVP/community line is understated and contains no invented dates.
- [ ] No client names, private projects, confidential systems or internal stakeholder names appear.
- [ ] No fabricated metrics, testimonials or achievements appear.
- [ ] No placeholder copy remains.
- [ ] Footer includes `Personal site. Views are my own.`

## 2. Privacy check

Search the entire production output, including assets and source:

- [ ] No email address is present.
- [ ] No phone number is present.
- [ ] No date of birth, age or family information is present.
- [ ] No precise home location is present.
- [ ] No financial, citizenship, health or travel information is present.
- [ ] No old `brauwers.nl` contact details are present.
- [ ] No historical `@ReneBrauwers` social link is present.
- [ ] No scraped social profile image is present.
- [ ] Image metadata has been stripped.
- [ ] No source map or comment exposes private information.
- [ ] No analytics or tracking identifier exists.

## 3. Visual review

Render and inspect screenshots at:

- [ ] 320 × 568
- [ ] 390 × 844
- [ ] 768 × 1024
- [ ] 1024 × 768
- [ ] 1440 × 900
- [ ] 1920 × 1080

At each size confirm:

- [ ] No horizontal overflow.
- [ ] Hero content is not clipped.
- [ ] H1 wraps intentionally and does not create isolated single words.
- [ ] Primary CTA is obvious.
- [ ] Headshot crop is natural, or monogram fallback looks deliberate.
- [ ] Business/Architecture/Engineering labels remain readable.
- [ ] Cards/columns collapse in a sensible order.
- [ ] Body line length remains comfortable.
- [ ] Section spacing feels deliberate, not empty or crowded.
- [ ] Footer does not overlap content.
- [ ] No focus ring or hover treatment is clipped.

Also inspect:

- [ ] 200% browser zoom.
- [ ] Dark operating-system mode, even if the site itself is light-only.
- [ ] High-contrast/forced-colour mode where available.
- [ ] Reduced-motion mode.
- [ ] A slow connection/mobile profile if a photographic asset is used.

## 4. Interaction and keyboard

- [ ] Skip link appears on focus and works.
- [ ] Focus order follows reading order.
- [ ] All controls are reachable by keyboard.
- [ ] Focus indicator is clearly visible.
- [ ] No keyboard trap.
- [ ] Anchor scroll lands with headings visible below any sticky header.
- [ ] LinkedIn link resolves to `https://www.linkedin.com/in/renebrauwers/`.
- [ ] X link resolves to `https://x.com/Rene_B`.
- [ ] JavaScript-disabled page remains fully readable and navigable.
- [ ] Reduced-motion preference disables non-essential movement.
- [ ] New-tab behaviour, if used, is communicated and secured.

## 5. Accessibility automation and manual checks

- [ ] Automated accessibility scan produces no serious or critical issue.
- [ ] Page has `lang="en-AU"`.
- [ ] Exactly one `h1`.
- [ ] Heading order is logical.
- [ ] Landmarks are present and correctly nested.
- [ ] Images have correct `alt`; decorative images use empty alt or are hidden.
- [ ] SVGs do not create noisy accessibility trees.
- [ ] Text and interactive-state contrast meet WCAG 2.2 AA.
- [ ] Touch targets meet 44 × 44px guidance.
- [ ] Information is not conveyed by colour alone.
- [ ] Link text makes sense out of context.
- [ ] No ARIA is used where native HTML is sufficient.

## 6. Technical validation

- [ ] HTML validates without material errors.
- [ ] CSS parses without material errors.
- [ ] Browser console has no errors or failed requests.
- [ ] No mixed-content request.
- [ ] No local absolute filesystem path.
- [ ] No development hostname.
- [ ] No 404 for site-owned assets.
- [ ] Favicon loads.
- [ ] Social card loads from its production path.
- [ ] Canonical URL is exactly `https://reneb.au/`.
- [ ] `robots.txt` is valid.
- [ ] `sitemap.xml` is valid and contains only the canonical homepage.
- [ ] JSON-LD parses and contains no private fields.
- [ ] External links use HTTPS.

## 7. Performance

Test the production build, not a development server with debugging overhead.

- [ ] Lighthouse Performance 95+ or documented reason.
- [ ] Lighthouse Accessibility 95+; aim for 100.
- [ ] Lighthouse Best Practices 95+.
- [ ] Lighthouse SEO 95+; aim for 100.
- [ ] LCP under 2.5 seconds.
- [ ] CLS under 0.1.
- [ ] INP under 200ms where measurable.
- [ ] Hero image has intrinsic dimensions.
- [ ] Hero/LCP image is not lazy-loaded.
- [ ] Below-fold photography is lazy-loaded.
- [ ] No remote font dependency.
- [ ] No unused large JavaScript or icon library.

## 8. Content feel test

Ask these questions during final visual inspection:

- [ ] Could this page belong to a random enterprise architect? If yes, make the progression from hands-on builder to decision advisor more distinctive.
- [ ] Does it sound like René challenges weak logic without sounding combative?
- [ ] Does the page lead with business consequences before technology?
- [ ] Could a business executive understand it without knowing architecture jargon?
- [ ] Would an engineer recognise that the advice remains close to delivery?
- [ ] Does the page avoid overclaiming?
- [ ] Is the visitor's next action obvious?

## 9. Release package

The handoff must include:

- [ ] deployable static output;
- [ ] source files;
- [ ] social card;
- [ ] favicon;
- [ ] short deployment instructions;
- [ ] validation summary with tested viewport sizes;
- [ ] screenshots or visual proof from desktop and mobile;
- [ ] list of optional items still awaiting René's input, if any.

The only expected optional item is an approved headshot or future public contact email. Neither may block a complete monogram-based version one.
