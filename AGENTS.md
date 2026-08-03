# Agent entry point

Read and follow [`agent.md`](./agent.md) in full before taking any implementation action.

`agent.md` is the authoritative project instruction file. The remaining files refine its requirements. If instructions conflict, apply this order:

1. The user's latest explicit instruction
2. `agent.md`
3. `DECISIONS.md`
4. `CONTENT.md`
5. `DESIGN.md`
6. `REQUIREMENTS.md`
7. `PRIVACY.md`
8. `SEO.md`
9. `QA.md`
10. `RESEARCH.md`

Do not skip the privacy or QA documents.

## Repository workflow

- Create or update the static site in `site/`; do not add a frontend framework or remote runtime unless the user explicitly changes the technical baseline.
- Use the root `compose.yaml` only for local preview and QA. Follow [`README.md`](./README.md) for the Docker commands and run every relevant lane in [`QA.md`](./QA.md).
- Treat `nginx/nginx.conf` as the application routing and security-header contract. Preserve `/healthz`, the canonical `/index.html` redirect and real `404` responses. The external edge proxy owns public TLS and canonical-host routing; verify those behaviours live after deployment rather than committing private edge configuration here.
- Publishing is performed by [`.github/workflows/publish-container.yml`](./.github/workflows/publish-container.yml). It validates `main` and publishes private GHCR tags; do not add tokens to the repository.
- Production is pull-only. Follow [`deploy/README.md`](./deploy/README.md) and deploy only `deploy/compose.yaml` plus a host-owned `.env`. Do not clone the repository or build the image on the production host.
- Never commit `.env` files, registry credentials, private hostnames, IP addresses or other infrastructure details.
