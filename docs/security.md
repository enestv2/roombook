# Security

## Secrets

- Secrets never enter the repo, specs, prompts, or chat. `.env` is gitignored; provide `.env.example`.
- Agents never print secret values, even when debugging.

## Input & output

Validate and normalize all client input at API boundaries and enforce domain rules server-side.
Use framework parameterization/ORM protections for PostgreSQL. Encode output through framework
defaults. Error responses never contain stack traces, SQL, secrets, or sensitive account details.

## AuthN / AuthZ

Use ASP.NET Core Identity for credential storage and password hashing. Enforce role-based
authorization server-side with default deny: members manage only their own reservations, while
administrators manage rooms, schedules, users, and all reservations. Use short-lived access tokens
with secure refresh handling; never trust role or owner values from the client.

## Dependencies

New dependencies require human approval and a check of license, maintenance health, transitive
dependencies, and known vulnerabilities. V1 avoids paid SaaS, external identity, and external
reservation services.

## Review lens

Security is a mandatory dimension of every independent review (see `prompts/review.md`), not a
separate afterthought phase.
