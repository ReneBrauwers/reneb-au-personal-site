# reneb.au personal site

The production source for [reneb.au](https://reneb.au/), René Brauwers' one-page personal website. It is a lightweight static site built with semantic HTML and CSS, served from a hardened non-root Nginx container.

The site deliberately has no analytics, cookies, form, CMS, frontend framework, remote fonts or client-side JavaScript.

## Docker development

Docker is the only local prerequisite.

```sh
docker compose up -d --build web
```

The local preview is available at `http://127.0.0.1:8091/`. Stop it with:

```sh
docker compose down
```

## Testing

Build the Docker-based browser test image, then run the validation lanes:

```sh
docker compose --profile qa build qa
docker compose --profile qa run --rm qa npm run html
docker compose --profile qa run --rm qa npm run css
docker compose --profile qa run --rm qa npm run validate
docker compose --profile qa run --rm qa npm run lighthouse
```

The browser suite covers the required responsive viewports, accessibility, keyboard navigation, reduced motion, forced colours, JavaScript-disabled behaviour, metadata, assets, security headers and real 404 handling. Screenshots and machine-readable results are written beneath `artifacts/`, which is intentionally ignored by Git.

For final HTTPS checks, override `QA_BASE_URL` with the public URL when running the QA container.

## Project structure

```text
site/              Deployable static website
nginx/             Production web-server configuration
design/            Source artwork for the social card
qa/                Docker-only validation and browser checks
Dockerfile         Pinned non-root production image
compose.yaml       Local preview, QA and deployment service definition
```

## Deployment approach

Production is built from the committed `main` branch with Docker Compose. The container publishes only to an explicitly configured host interface, and the existing edge reverse proxy owns public HTTP, HTTPS, TLS and canonical-host redirects. Environment-specific addresses are set on the deployment host and are never committed.

Unknown paths return a real `404`; `/index.html` permanently redirects to the canonical `/` path.

## Content and privacy

The site publishes only approved professional information and links to René's LinkedIn and X profiles. It contains no public email address, employer branding, private infrastructure information, tracking or third-party embeds.
