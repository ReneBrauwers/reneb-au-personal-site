# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

- Public professional visitors evaluating René Brauwers' point of view and demonstrated technology-leadership evidence.
- Executive recruiters and hiring leaders assessing a specific senior technology or enterprise-architecture mandate. Private detail is available only after mailbox verification and, where required, approval.
- René as the administrator who governs published content, recruiter access, private messages, résumé grants and security-sensitive settings.

## Product Purpose

reneb.au helps decision-makers understand where René can improve consequential technology decisions, then gives verified recruiters a private path from broad discovery to a specific, evidence-led conversation. Success means relevant conversations start with decision rights, outcomes, operating constraints and demonstrated evidence—not a generic keyword match.

## Positioning

The product connects public professional evidence to a governed private opportunity workflow. Mandate Lens is its distinctive mechanism: it deterministically tests a recruiter's own role brief against candidate-supplied published evidence, surfaces ambiguity and operating-model friction, refuses a fit score, and lets the recruiter decide whether to share the brief privately.

## Operating Context

- Public discovery begins on the portfolio, recruiter profile, machine-readable candidate profile and approved search surfaces.
- A recruiter verifies mailbox ownership, may require approval, and enters a non-indexed, non-analytics private portal.
- In the private portal, the recruiter can inspect exact opportunity guidance, run Mandate Lens without persistence, explicitly share a bounded brief as an encrypted inbound message, request résumé access or leave ordinary private context.
- René administers content drafts and publication, recruiter lifecycle, inbox, résumé grants, retention and optional draft-only authoring from TOTP-protected routes.
- Production is a pull-only Docker deployment. Hardened Nginx is the only exposed service; the ASP.NET Core Razor Pages portal remains internal.

## Capabilities and Constraints

- Public and private content are server rendered. The public homepage remains framework-free HTML/CSS; the governed portal uses ASP.NET Core Razor Pages and encrypted SQLite storage.
- Private recruiter data, messages, résumé material and security-sensitive values are encrypted server-side. Private routes are non-cacheable, non-indexable and do not load analytics.
- Passwordless mailbox verification establishes identity. Administrator functions additionally require a current six-digit TOTP code.
- Mandate Lens runs on the application host, calls no external AI service, uploads nothing to a third party, persists nothing during analysis, produces no percentage or automated employment decision, and uses only published candidate evidence.
- Sharing is a separate explicit action. It stores the complete bounded mandate, optional recruiter context and a concise lens summary as an encrypted inbound message; notification email contains no message body.
- Provider-assisted authoring, when enabled, can propose validated drafts only. A human administrator remains the only publication authority.
- No public copy may fabricate employers, clients, outcomes, testimonials, credentials, compensation, availability or other evidence.

## Brand Commitments

- Product and site name: `reneb.au`; public identity: René Brauwers.
- Voice is business-first, direct, measured and evidence-led. Technology is discussed through decisions, outcomes, risk and delivery reality rather than vendor theatre.
- The established public-content authority is `CONTENT.md`; the incumbent visual authority is `DESIGN.md`.
- The site invites relevant professional conversation without becoming a broad consulting funnel, job board or automated matching product.

## Evidence on Hand

- `CONTENT.md` contains approved public copy and the private Mandate Lens content boundary.
- Published candidate profiles provide the only evidence Mandate Lens may quote.
- `REQUIREMENTS.md`, `PRIVACY.md` and `QA.md` define functional, privacy and release acceptance.
- `DESIGN.md` records the established visual and responsive system.
- The repository contains no approved third-party testimonials or employer endorsements; future work must not invent them.

## Product Principles

1. Start with the consequential decision and business outcome.
2. Put candidate-supplied evidence before fit claims.
3. Preserve human judgement; never turn ambiguity into a score.
4. Make private processing and explicit consent visible at the moment they matter.
5. Keep public discovery useful while maintaining a hard boundary around recruiter and opportunity detail.

## Accessibility & Inclusion

The product targets WCAG 2.2 AA with semantic server-rendered HTML, keyboard operation, visible focus, reduced-motion support, accessible validation and minimum 44-pixel interactive targets. Responsive acceptance includes narrow mobile, touch, tablet and wide desktop layouts without root horizontal scrolling.
