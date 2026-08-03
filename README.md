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
deploy/            Pull-only production Compose manifest and runbook
Dockerfile         Pinned non-root production image
compose.yaml       Local preview and QA service definition
```

## Deployment approach

GitHub Actions validates `main`, builds the production container and publishes private `latest` and immutable `sha-*` tags to GHCR. Production holds only the pull-only Compose manifest and its host-specific `.env`; it does not contain a repository checkout, source files or build scripts.

The production manifest uses `pull_policy: always`, so each deployment checks GHCR for the newest selected image. `RENEB_AU_IMAGE` defaults to `ghcr.io/renebrauwers/reneb-au-personal-site:latest` and can be changed to an immutable tag or digest for a deliberate pin or rollback. See [`deploy/README.md`](deploy/README.md) for the operator workflow.

The container publishes only to an explicitly configured host interface, and the existing edge reverse proxy owns public HTTP, HTTPS, TLS and canonical-host redirects. Environment-specific addresses and registry credentials are never committed.

Unknown paths return a real `404`; `/index.html` permanently redirects to the canonical `/` path.

## Content and privacy

The site publishes only approved professional information and links to René's LinkedIn and X profiles. It contains no public email address, employer branding, private infrastructure information, tracking or third-party embeds.
