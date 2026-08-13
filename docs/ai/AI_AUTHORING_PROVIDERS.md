# AI authoring provider setup

AI authoring is optional, administrator-only and draft-only. It has no tools, autonomous browsing, recruiter/message/audit access or publication authority. Keep `AI_EGRESS_ENABLED=false` until Content Studio editing, migration, backup and visual acceptance pass.

## Host prerequisite

Create and mount the independent `ai-credential-keyring.json` described in [`../../deploy/README.md`](../../deploy/README.md). Do not put OpenRouter/xAI API keys in `.env`, Compose, source, shell history or a provider setup screenshot. Production Compose mounts the keyring at `/run/secrets/ai-credential-keyring.json`; the portal encrypts provider keys before SQLite storage and shows only their fingerprint and last four characters.

## Administrator workflow

1. Set `AI_EGRESS_ENABLED=true`, recreate the matching gateway/portal image pair and verify `/readyz`.
2. Sign in at `/auth/admin`, complete TOTP and open `/admin/ai/providers`.
3. Set the site-wide monthly USD ceiling first.
4. Enter one provider API key. Saving/replacing/deleting a key requires TOTP within five minutes and invalidates the previous provider test.
5. Refresh compatible models. The portal uses authenticated, user-specific discovery and keeps text-in/text-out models capable of strict structured output; cache expiry is one hour.
6. Select one model, configure a lower-or-equal provider monthly cap and a per-request maximum output-token value.
7. Run the minimal structured-output test. Authoring becomes available only after it succeeds. Authentication/permission failures disable the provider; temporary availability/rate errors mark it degraded. A removed model disables the configuration.
8. Review the displayed retention observation. xAI reports `x-zero-data-retention`; OpenRouter requests enforce `data_collection=deny` but do not claim a ZDR header that was not returned.

There is no automatic cross-provider fallback. Select the intended provider/model for each conversation so private-context disclosure and cost attribution remain explicit.

## OpenRouter

- Create a dedicated API key with the least account scope supported by the OpenRouter account.
- Discovery: authenticated `GET /api/v1/models/user`.
- Inference: `POST /api/v1/chat/completions` with strict JSON Schema.
- Every call sets `provider.require_parameters=true` and `provider.data_collection=deny` so only endpoints supporting the request parameters and denying provider data collection are eligible.
- Verify account-level privacy/guardrail settings before selecting private context. The portal does not infer that `data_collection=deny` is the same as an observed ZDR response.

References: [user-filtered model discovery](https://openrouter.ai/docs/api/api-reference/models/list-models-user), [structured output](https://openrouter.ai/docs/guides/features/structured-outputs), [provider routing and data policy](https://openrouter.ai/docs/guides/routing/provider-selection).

## xAI

- Create a dedicated xAI API key for this site/team.
- Discovery: authenticated `GET /v1/language-models` and local text-modality filtering.
- Inference: `POST /v1/responses` with `store=false`, no tools and `text.format.type=json_schema`.
- By default xAI documents 30-day encrypted API input/output retention for abuse review. ZDR is a team-level external setting. The portal records and displays the `x-zero-data-retention` response header but cannot enable ZDR itself.
- Actual charged cost is reconciled from `usage.cost_in_usd_ticks` using 10^10 ticks per USD.

References: [language model discovery](https://docs.x.ai/developers/rest-api-reference/inference/models), [Responses API comparison](https://docs.x.ai/developers/model-capabilities/text/comparison), [structured output](https://docs.x.ai/developers/model-capabilities/text/structured-outputs), [cost tracking](https://docs.x.ai/developers/cost-tracking), [retention and ZDR](https://docs.x.ai/developers/faq/security).

## Context and privacy

The context library accepts validated PDF, DOCX, UTF-8 TXT and Markdown, with a 10 MB file and 50 MB total limit. Original bytes, filenames and extracted text are encrypted locally. The portal never uploads provider-native files; only selected extracted text is sent.

Public target content is sent only when selected. The private opportunity profile, active résumé and every context upload require a per-request acknowledgement. The disclosure must be read against the selected provider/model and displayed retention posture. Treat uploaded text as untrusted evidence; embedded instructions do not change system instructions or grant tools.

Delete conversations or context immediately when no longer required. Conversations also expire after 30 inactive days. Usage/audit records keep cost, tokens, provider request ID and observed ZDR metadata only—not prompts or responses.

## Verification and rotation

- Use fake-service contract tests in CI; do not make billable provider calls from automated pull-request tests.
- Before production use, run one minimal connection test and one non-sensitive draft proposal, inspect its field diff, apply it to a draft and prove it cannot publish without recent TOTP.
- Confirm provider keys, prompts, responses and uploaded content do not appear in logs, HTML, analytics, image layers or plaintext database values.
- Rotate by entering the replacement key and repeating discovery, model selection and connection testing. Delete the old key at the provider after the portal reports the new key ready.
- If a key may be compromised, delete/disable it at the provider first, then remove the portal configuration and inspect metadata-only provider usage/audit records.
