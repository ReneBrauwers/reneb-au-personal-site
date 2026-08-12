# Pull-only production deployment

Production receives only this directory's `compose.yaml`, a host-owned `.env`, host-owned secret files and Docker-managed data/backup volumes. It must not contain a repository checkout, source, SDK, Dockerfile, test tooling or build scripts.

## 1. Registry access

Both GHCR packages are private. Use a dedicated GitHub machine identity with package read access and a classic token limited to `read:packages`:

```sh
printf '%s' "$GHCR_TOKEN" | docker login ghcr.io --username "$GITHUB_USER" --password-stdin
unset GHCR_TOKEN
```

Restrict the deployment account's Docker configuration. Before launch, require GitHub's package API to report both packages as `private` and prove that full anonymous pulls fail specifically for authorization—not merely because of a network, rate-limit or missing-tag error. A bare registry-manifest `401` is insufficient because public OCI registries also use that response to start anonymous bearer-token authentication.

## 2. Host-owned configuration

Copy `.env.example` to `.env`, set mode `0600`, and replace every example value. Do not put tokens, certificate material or encryption keys in `.env`.

Keep both image references on matching tags:

```dotenv
RENEB_AU_IMAGE=ghcr.io/renebrauwers/reneb-au-personal-site:latest
RENEB_AU_PORTAL_IMAGE=ghcr.io/renebrauwers/reneb-au-recruiter-portal:latest
RECRUITER_PORTAL_ENABLED=false
```

Use `sha-<full-commit>` for the initial release, a controlled release or rollback. `pull_policy: always` applies to both services even when an immutable tag is selected.

CI always publishes both immutable SHA tags before considering either `latest` pointer. Two separate GHCR packages do not provide a cross-package atomic tag transaction. The workflow therefore refuses to auto-promote if either `latest` tag is absent. Once both pointers are initialized, it advances the pair and attempts to restore both previous pointers if a promotion command fails. Production acceptance must always prove that the two resolved OCI revision labels match before enabling discovery.

For the first two-image release, keep production pinned to the matching immutable SHA pair. Dispatch the `Publish container` workflow from the accepted `main` commit with `initialize_latest=true`. The workflow uses its repository-scoped package write permission, promotes the absent tag first, then the existing tag, and records `Latest channel: initialized`.

If workflow dispatch is unavailable, a trusted administrative workstation with package write permission can deliberately seed both mutable tags from that accepted pair:

```sh
docker buildx imagetools create --prefer-index=false --tag ghcr.io/renebrauwers/reneb-au-personal-site:latest ghcr.io/renebrauwers/reneb-au-personal-site:sha-<full-commit>
docker buildx imagetools create --prefer-index=false --tag ghcr.io/renebrauwers/reneb-au-recruiter-portal:latest ghcr.io/renebrauwers/reneb-au-recruiter-portal:sha-<full-commit>
```

If either command fails, leave production pinned and retry until both tags resolve to images whose OCI revision label is the same full commit. Only then may `.env` return to the `latest` defaults. GitHub exposes package-version deletion rather than a safe tag-only rollback, so absence is not represented by deleting a newly tagged version: that version also owns the immutable SHA tag.

`ADMIN_EMAILS` is a comma-separated set of verified mailbox addresses. It is the live administrator allowlist: removing an address removes its admin authority on the next authorization check.

`UNTRUSTED_EMAIL_DOMAINS` is the comma-separated set of consumer/free-mail domains that remain pending for administrator approval after mailbox verification. `DISPOSABLE_EMAIL_DOMAINS` is the separate comma-separated set rejected with a generic response. Domain and subdomain matches are case-insensitive. Both lists are configurable and non-exhaustive; every other non-disposable domain activates after mailbox verification and can still be suspended by an administrator.

`PORTAL_TRUSTED_PROXY_NETWORKS` is a comma-separated CIDR allowlist for the actual Docker gateway and external edge-proxy hops. Resolve these networks read-only on the production host; do not copy the documentation examples. The portal processes at most two forwarded hops and stops when the current sender is not trusted, preventing a client-supplied `X-Forwarded-For` value from bypassing IP throttling.

## 3. Field-encryption keyring

Create the secret directory with mode `0700`. Generate two independent 32-byte random values in a password-manager-backed administrative session and create the following JSON without printing either value or the file contents to a shared terminal/log:

```json
{
  "activeKeyId": "v1",
  "lookupKey": "BASE64_OF_A_DIFFERENT_32_RANDOM_BYTES",
  "keys": {
    "v1": "BASE64_OF_EXACTLY_32_RANDOM_BYTES"
  }
}
```

Point `PORTAL_KEYRING_FILE` at that absolute host path. The lookup key makes normalized email/code hashes stable when the active encryption key changes; do not replace it without an atomic hash-migration procedure. Back up the keyring separately from the encrypted database backup. Never remove an older key ID while database fields or backups still reference it; rotate field encryption by adding a new key and changing `activeKeyId`, then re-encrypt data through a reviewed migration.

## 4. Microsoft 365 sender

Provision a dedicated Exchange Online mailbox and an Entra application using certificate credentials. Upload only the public certificate to Entra; mount its PEM certificate and private key as host files. Set the tenant ID, client ID and dedicated sender address in `.env`.

Grant `Application Mail.Send` through Exchange Online Application RBAC with a resource scope containing only the dedicated mailbox. Do **not** also grant the Microsoft Graph `Mail.Send` application permission in Entra: the two authorization systems are additive, so that Entra grant would restore tenant-wide access. Validate the scoped application authorization for the sender mailbox and a different mailbox; the first must be in scope and the second out of scope. Query the enterprise service principal's `appRoleAssignments` as the authoritative Entra grant check; an empty app-registration `requiredResourceAccess` manifest is necessary but insufficient. Do not enable the portal based only on successful token acquisition.

The complete ClickOps, Azure CLI, Exchange Online PowerShell, rotation and verification instructions are in [`../docs/entra/RECRUITER_PORTAL_MAIL_IDENTITY.md`](../docs/entra/RECRUITER_PORTAL_MAIL_IDENTITY.md).

Provision a separate self-signed RSA certificate/key pair for ASP.NET Data Protection and mount it through the `DATA_PROTECTION_*` paths. Do not reuse the Graph credential. The private key protects persisted cookie-key material in the data volume. Retain the old certificate/private key and test cookie-key recovery before rotating this pair; deleting it while old Data Protection keys remain makes active cookies and protected state unreadable.

The portal runs as UID/GID `10001`. Standalone Docker Compose bind-mounts file-backed secrets without remapping ownership, so make each of the five secret files owned by `10001:10001` and mode `0400`; keep the parent directory root-owned and non-listable to other users. Verify readability as UID 10001 before launch without printing the contents. Compose mounts them at the exact `.json`/`.pem` filenames configured under `/run/secrets`.

References:

- [Microsoft Graph sendMail](https://learn.microsoft.com/en-us/graph/api/user-sendmail?view=graph-rest-1.0)
- [Exchange Online Application RBAC](https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac)

## 5. First start while discovery is disabled

Validate and pull:

```sh
docker compose --env-file .env -f compose.yaml config
docker compose --env-file .env -f compose.yaml pull
```

Create the schema before the first long-running start:

```sh
docker compose --env-file .env -f compose.yaml run --rm portal migrate </dev/null
docker compose --env-file .env -f compose.yaml up -d --force-recreate portal web --wait
```

Recreate `portal` and `web` together. Nginx resolves its upstream when it starts, so recreating only the portal can leave the gateway holding the previous container address.

Confirm `/healthz` succeeds and `/readyz` reports ready. Recruiter discovery and ordinary sign-in should return `404` while disabled, but an administrator can request a magic link at `/auth/admin`, configure TOTP and reach `/admin`.

Through the admin UI:

1. enrol TOTP and store a recovery copy of the seed in the password manager;
2. review/edit the public draft and publish it;
3. enter exact compensation, detailed availability and opportunity criteria in the encrypted private editor;
4. upload and validate the PDF résumé; and
5. test Graph delivery to an external mailbox, business/free/disposable domain flows, approval, message, résumé grant/download/revocation and account deletion.

## 6. Normal release

Do not migrate or recreate before obtaining and verifying a backup:

```sh
docker compose --env-file .env -f compose.yaml pull
docker compose --env-file .env -f compose.yaml run --rm portal backup </dev/null
docker compose --env-file .env -f compose.yaml run --rm portal restore-check </dev/null
docker compose --env-file .env -f compose.yaml run --rm portal migrate </dev/null
docker compose --env-file .env -f compose.yaml up -d --remove-orphans --force-recreate portal web --wait
```

The backup command uses SQLite's online backup API and AES-GCM encryption. `restore-check` selects the newest encrypted backup unless an explicit container path is supplied, decrypts it to temporary storage and requires `PRAGMA integrity_check` to pass.

Verify the resolved images, health, image IDs/digests and OCI revision labels:

```sh
docker compose --env-file .env -f compose.yaml config --images
docker compose --env-file .env -f compose.yaml ps
docker inspect "$(docker compose --env-file .env -f compose.yaml ps -q web)" --format '{{.Image}} {{index .Config.Labels "org.opencontainers.image.revision"}}'
docker inspect "$(docker compose --env-file .env -f compose.yaml ps -q portal)" --format '{{.Image}} {{index .Config.Labels "org.opencontainers.image.revision"}}'
```

Then run the production browser lane with `QA_BASE_URL=https://reneb.au` and verify live TLS, apex/www/HTTP canonical redirects, real 404s, headers, touch behaviour, persistence after restart, external mail and zero Umami requests on private pages.

## 7. Enable discovery

Only after the disabled-mode acceptance above, set:

```dotenv
RECRUITER_PORTAL_ENABLED=true
```

Recreate both services and repeat public discovery, private leakage, authentication and browser acceptance. This is the atomic public launch: no source/image change is required.

## 8. Rollback

First set `RECRUITER_PORTAL_ENABLED=false` and recreate both services. Then pin both image values to the previous matching full-SHA tags, pull and recreate `portal` and `web` together. Do not mix gateway and portal commit tags.

Migrations follow expand/contract compatibility, so the previous portal should run on the migrated database. Restore a database backup only after a specific compatibility diagnosis and a separate destructive-change approval; a restore can discard post-backup recruiter activity.

## 9. Backup operations

Encrypted backups remain in the `portal-backups` volume and are useless without an appropriate field keyring. Copy both into independently protected backup storage, test restore verification periodically and record retention. Keep production `.env`, registry credentials, certificate private key and keyring out of routine logs and support bundles.
