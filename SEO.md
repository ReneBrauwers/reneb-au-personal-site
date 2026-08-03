# SEO, sharing and identity

The objective is accurate identity resolution and a strong professional preview—not aggressive search marketing.

## Primary search intent

The page should answer searches such as:

- René Brauwers
- Rene Brauwers
- René Brauwers enterprise architecture
- Rene Brauwers Perpetual Corporate Trust
- Rene Brauwers Sydney
- Rene Brauwers business technology advisor

Use the accented name in visible copy and include the ASCII spelling naturally in structured metadata only where useful. Do not stuff variants into body text.

## Required head metadata

```html
<title>René Brauwers — Business Technology Advisor &amp; Enterprise Architecture Leader</title>
<meta
  name="description"
  content="René Brauwers helps Business and Engineering make better technology decisions, connecting strategy, risk, architecture and delivery in regulated financial services."
>
<link rel="canonical" href="https://reneb.au/">
<meta name="robots" content="index,follow,max-image-preview:large">
<meta name="theme-color" content="#0A1624">
```

Add standard character set and responsive viewport declarations.

## Open Graph

```html
<meta property="og:type" content="profile">
<meta property="og:site_name" content="René Brauwers">
<meta property="og:title" content="René Brauwers — Better technology decisions">
<meta
  property="og:description"
  content="Business-first technology advice and enterprise architecture that connects strategy, risk and engineering reality."
>
<meta property="og:url" content="https://reneb.au/">
<meta property="og:image" content="https://reneb.au/social-card.png">
<meta property="og:image:width" content="1200">
<meta property="og:image:height" content="630">
<meta property="og:image:alt" content="René Brauwers — Better technology decisions. Stronger business outcomes.">
<meta property="profile:first_name" content="René">
<meta property="profile:last_name" content="Brauwers">
```

Adjust the image filename to the actual production asset. Use an absolute HTTPS URL.

## X/Twitter card

```html
<meta name="twitter:card" content="summary_large_image">
<meta name="twitter:creator" content="@Rene_B">
<meta name="twitter:title" content="René Brauwers — Better technology decisions">
<meta
  name="twitter:description"
  content="Business-first technology advice and enterprise architecture that connects strategy, risk and engineering reality."
>
<meta name="twitter:image" content="https://reneb.au/social-card.png">
<meta name="twitter:image:alt" content="René Brauwers — Better technology decisions. Stronger business outcomes.">
```

Do not use the historical `@ReneBrauwers` handle.

## JSON-LD

Use a conservative `Person` object:

```json
{
  "@context": "https://schema.org",
  "@type": "Person",
  "@id": "https://reneb.au/#rene-brauwers",
  "name": "René Brauwers",
  "alternateName": "Rene Brauwers",
  "url": "https://reneb.au/",
  "jobTitle": "Head of Enterprise Architecture",
  "worksFor": {
    "@type": "Organization",
    "name": "Perpetual Corporate Trust",
    "url": "https://www.perpetual.com.au/corporate-trust/"
  },
  "homeLocation": {
    "@type": "Place",
    "name": "Sydney, Australia"
  },
  "sameAs": [
    "https://www.linkedin.com/in/renebrauwers/",
    "https://x.com/Rene_B"
  ],
  "knowsAbout": [
    "Enterprise architecture",
    "Business technology strategy",
    "Technology decision-making",
    "Responsible AI",
    "Cloud architecture",
    "Enterprise integration",
    "Automation",
    "Regulated financial services technology"
  ]
}
```

Rules:

- Do not include a street address, birth date, email or phone.
- Do not use `award` until wording and dates are approved.
- Do not include employer logos.
- If the employer URL changes, update it from an official source.
- `homeLocation` means broad location only; never use a specific suburb or precise address.

## Heading and copy signals

- The `h1` is the proposition, not merely the name.
- René's full name must appear as visible text near the hero.
- Current role and location should appear in the page copy.
- Use “enterprise architecture” naturally but do not repeat it unnaturally.
- Mention regulated financial services once or twice.
- Do not add hidden keyword blocks.

## `robots.txt`

```text
User-agent: *
Allow: /

Sitemap: https://reneb.au/sitemap.xml
```

## `sitemap.xml`

Include only the canonical homepage in version one. Use the actual release date for `lastmod`.

## Social-card quality

Validate:

- 1200 × 630 exact or equivalent accepted ratio;
- text remains inside safe margins;
- contrast remains strong at small preview sizes;
- no fine-line motif disappears when compressed;
- no employer or vendor logo;
- correct accent in René;
- image under practical platform size limits;
- absolute production URL returns HTTP 200.

## Indexing and redirects

When the domain is connected:

- redirect HTTP to HTTPS;
- choose `https://reneb.au/` as canonical;
- redirect `www.reneb.au` to the apex if configured;
- avoid duplicate index paths such as `/index.html`;
- return a real 404 for unknown paths rather than the homepage with a 200;
- do not redirect old `brauwers.nl` domains unless René controls them and explicitly requests it.

## Search-console follow-up

Optional after deployment:

- verify the apex domain in Google Search Console and Bing Webmaster Tools;
- submit the sitemap;
- inspect the rendered URL;
- check social previews with platform debugging tools;
- confirm the old domains are not creating unwanted canonical conflicts.

These are deployment tasks, not prerequisites for the static build.
