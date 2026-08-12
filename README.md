# reneb.au

Production source for [reneb.au](https://reneb.au/): René Brauwers' public portfolio, evidence-led recruiter discovery and private candidate portal.

The homepage remains semantic static HTML/CSS. A private ASP.NET Core 10 Razor Pages service provides the recruiter preview, structured machine representations, passwordless mailbox verification, encrypted opportunity details, inbound messages, approval-controlled résumé access and administration. Hardened Nginx is the only exposed container.

## Local development

Docker is the only runtime prerequisite:

```sh
docker compose up -d --build portal web
```

Open `http://127.0.0.1:8091/`. Development mail is captured in the local database and exposed only inside the Development portal at `/dev/mail`; it is not routed through Nginx. Local data and backups use named Docker volumes.

Stop the stack with:

```sh
docker compose down
```

Use `docker compose down --volumes` only when intentionally destroying local portal state.

## Validation

```sh
docker compose build web portal
docker run --rm --volume "$PWD:/src" --workdir /src mcr.microsoft.com/dotnet/sdk:10.0.400-noble dotnet test portal.tests/ReneB.Portal.Tests.csproj --configuration Release
docker compose --profile qa build qa
docker compose --profile qa run --rm qa npm run html
docker compose --profile qa run --rm qa npm run css
docker compose --profile qa run --rm qa npm run validate
docker compose --profile qa run --rm qa npm run lighthouse
```

`QA.md` is the full acceptance contract. The suite includes public/private leakage, authentication, authorization, résumé grants, browser accessibility, 320–1440 px responsive checks and Chromium/WebKit touch movement.

## Repository map

```text
site/                 Static public portfolio
portal/               ASP.NET Core portal, encrypted SQLite data and operational commands
portal.tests/         Behaviour and security tests
nginx/                Public routing and header contract
qa/                   Dockerised HTML/CSS/Playwright/Lighthouse lanes
deploy/               Pull-only production Compose example and runbook
docs/adr/             Architecture decisions
AGENTS.md, agent.md    AI-agent entry point and workflow
CONTENT.md             Approved public copy and recruiter discovery boundaries
DESIGN.md              Visual and responsive contract
REQUIREMENTS.md        Functional and non-functional contract
PRIVACY.md             Publication, collection and retention rules
SEO.md                 Human and machine discovery contract
QA.md                  Required validation and release evidence
```

## Publishing and deployment

Pushes to `main` run all gates and publish matching private GHCR images:

- `ghcr.io/renebrauwers/reneb-au-personal-site`
- `ghcr.io/renebrauwers/reneb-au-recruiter-portal`

Each always receives a matching `sha-<full-commit>` tag with SBOM/provenance. After the two-package `latest` channel has been deliberately initialized, CI advances both mutable tags and restores the previous pair if a later promotion command fails. Production is pull-only and never clones or builds this repository. Both Compose services use `pull_policy: always`; the initial launch and any controlled release or rollback pin both images to the same immutable tag.

See [`deploy/README.md`](deploy/README.md) for secret provisioning, backup/migration, release and rollback. Begin with `RECRUITER_PORTAL_ENABLED=false`; enable discovery only after Graph mail, admin/TOTP, both profiles, résumé and full browser acceptance pass.

## Privacy boundary

Only approved professional evidence and broad opportunity-fit signals are public. Exact compensation, detailed availability, recruiter contact data, messages, résumé bytes and security material are encrypted server-side and never baked into images or emitted to analytics. Private pages are non-cacheable, non-indexable and do not load Umami.
