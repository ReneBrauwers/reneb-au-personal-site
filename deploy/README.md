# Production deployment

Production runs the published GHCR image. It does not clone this repository and does not build the image locally.

Copy only `compose.yaml` and a host-specific `.env` into an operator-owned deployment directory. The `.env` file must be mode `0600`; it contains deployment settings, not the GHCR token.

## GHCR authentication

The image package is private. Authenticate Docker as a GitHub identity that has package read access, using a classic personal access token with the `read:packages` scope:

```sh
printf '%s' "$GHCR_TOKEN" | docker login ghcr.io --username GITHUB_USER --password-stdin
unset GHCR_TOKEN
```

Docker stores the credential outside the deployment directory. Restrict the resulting Docker configuration to the deployment account.

## Image selection

The default `.env` setting follows the newest successful `main` build:

```dotenv
RENEB_AU_IMAGE=ghcr.io/renebrauwers/reneb-au-personal-site:latest
```

For a deliberate pin or rollback, replace it with an immutable commit tag or digest:

```dotenv
RENEB_AU_IMAGE=ghcr.io/renebrauwers/reneb-au-personal-site:sha-FULL_COMMIT_SHA
```

The Compose service has `pull_policy: always`. Every deployment checks the registry even when the selected tag already exists locally.

## Deploy

Validate the resolved configuration, pull before changing the running container, then recreate and wait for health:

```sh
docker compose --env-file .env -f compose.yaml config
docker compose --env-file .env -f compose.yaml pull
docker compose --env-file .env -f compose.yaml up -d --remove-orphans --wait
```

Confirm the configured image, health and revision labels after deployment:

```sh
docker compose --env-file .env -f compose.yaml config --images
docker compose --env-file .env -f compose.yaml ps
docker inspect "$(docker compose --env-file .env -f compose.yaml ps -q web)" \
  --format '{{ index .Config.Labels "org.opencontainers.image.revision" }}'
```
