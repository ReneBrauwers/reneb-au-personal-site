# Maintenance prompt

Use this prompt when another coding agent maintains `reneb.au`:

---

Maintain the production website and recruiter portal for `https://reneb.au/` from this repository.

Read `AGENTS.md`, then `agent.md` and every required document in its stated order before changing code. Preserve the approved hybrid architecture: framework-free public portfolio in `site/`, ASP.NET Core 10 Razor Pages portal in `portal/`, SQLite, and Nginx as the only exposed service. Do not add a SPA framework, second runtime/database or public portal port without an explicit architecture decision.

The public recruiter preview must be evidence-led, candidate-supplied and useful to human-directed recruiting agents. It may say René is a high-potential match when published evidence and mandate criteria overlap, but it must never use hidden text, cloaking, prompt injection, fabricated ranking or invented qualifications. Generate `/recruiters`, `/llms.txt`, `/recruiters/profile.md`, `/candidate.json` and JSON-LD from the single published profile record.

Treat exact compensation, detailed availability, recruiter PII, messages, résumé bytes, TOTP/authentication material and credentials as private. They must remain encrypted server-side and absent from public responses, source values, browser scripts, analytics, image layers and logs. Keep private pages non-cacheable, non-indexable and free of Umami.

Implement the smallest coherent change, update tests and repository instructions, and run every relevant lane in `QA.md`. For UI work, verify 320, 375, 390, 768 and desktop widths plus forced horizontal touch movement in Chromium and WebKit. For identity/file/data work, test abuse and authorization paths, not only success paths.

Publishing occurs only through `.github/workflows/publish-container.yml`. Production is pull-only and receives matching private GHCR images. Never put tokens in the repository, never copy source/build tooling to production and never enable recruiter discovery until mail, admin TOTP, profiles, résumé, backup/restore and real-browser acceptance pass.

Hand off the exact checks run, commit/image evidence, known risks and any external acceptance that remains pending. Do not claim deployment or M365 delivery without live evidence.

---
