# ADR-002: Reverse recruiter-domain classification and use direct Exchange application RBAC

- Status: Accepted
- Date: 2026-08-13
- Supersedes: ADR-001 domain-classification decision only

## Context

The original portal activated only explicitly allowlisted business domains. René wants lower friction for recruiters using ordinary organisation domains while retaining review for common consumer mail and rejecting known disposable mail. Domain reputation lists are incomplete and cannot prove ownership, business legitimacy or intent.

Microsoft now documents Exchange Online Application RBAC as a resource-scoped authorization source independent of Entra application permissions. The grants are additive: combining a mailbox-scoped Exchange role with an unscoped Entra Graph `Mail.Send` grant removes the practical mailbox boundary.

## Decision

After mailbox verification:

- configured disposable domains are rejected generically and no account is stored;
- configured consumer/free-mail domains become `PendingApproval`; and
- every other non-disposable domain becomes active, subject to immediate administrator suspension.

`UNTRUSTED_EMAIL_DOMAINS` and `DISPOSABLE_EMAIL_DOMAINS` are host-owned, comma-separated lists. Exact domains and their subdomains match case-insensitively. The shipped defaults are a starting point, not an exhaustive reputation service.

Production mail uses a dedicated certificate-only Entra service principal with no unscoped Graph application permission. Exchange Online assigns `Application Mail.Send` directly to that principal through a custom resource scope matching only the dedicated sender mailbox.

## Alternatives considered

### Retain the positive business-domain allowlist

- Stronger default screening and least privilege.
- Higher operational friction because every new recruiter organisation requires configuration or approval.
- Rejected by the latest product direction.

### Automatically trust every mailbox-verified domain

- Lowest friction.
- Treats consumer, anonymous and disposable providers as equivalent to organisation domains.
- Rejected because it removes useful screening and abuse controls.

### Grant Graph `Mail.Send` in Entra and add Exchange scoping

- Familiar Entra permission workflow.
- Unsafe for this boundary because authorization grants are additive and the Entra grant remains organization-wide.
- Rejected in favour of direct Exchange Application RBAC.

## Consequences

Positive:

- legitimate organisation-domain recruiters enter without per-domain administration;
- consumer providers remain visible for human approval;
- disposable providers receive a generic denial; and
- the mail service principal has a verifiable one-mailbox authorization boundary.

Trade-offs and risks:

- any attacker controlling an unlisted domain can activate after mailbox verification;
- domain lists age and require operational review;
- domain classification is a screening signal, not identity assurance; and
- Exchange RBAC changes can take time to propagate even when the authorization test cmdlet succeeds immediately.

Mitigations include rate limiting, mailbox proof, metadata-only audit, administrator suspension, configurable lists, short sessions and testing both an in-scope and out-of-scope mailbox before enabling discovery.

## Review triggers

Revisit this decision if abuse rises, recruiter volume warrants a reputation service, the site adds autonomous data access, or Microsoft changes Exchange Application RBAC semantics.
