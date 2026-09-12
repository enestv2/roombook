# Security

## Secrets

- Secrets never enter the repo, specs, prompts, or chat. `.env` is gitignored; provide `.env.example`.
- Agents never print secret values, even when debugging.

## Input & output

Validate and normalize all client input at API boundaries and enforce domain rules server-side.
Use framework parameterization/ORM protections for PostgreSQL. Encode output through framework
defaults. Error responses never contain stack traces, SQL, secrets, or sensitive account details.

## AuthN / AuthZ

Use ASP.NET Core Identity (or the approved production identity provider) for credential storage and
password hashing. Enforce role-based authorization server-side with default deny: members manage
only their own reservations, while administrators manage rooms, schedules, users, and all
reservations. The Development environment has an explicit `X-Development-Member-Id` authentication
path that creates only a `Member` principal; it is not registered in production. Use short-lived
access tokens with secure refresh handling in production; never trust role or owner values from the
client. The development client can set `VITE_DEVELOPMENT_MEMBER_ID` to send that header locally.

- Production uses ASP.NET Core Identity backed by PostgreSQL. The `/api/auth` Identity API endpoints
  issue short-lived opaque bearer tokens (not JWTs) and refresh tokens; configure `ConnectionStrings:Roombook` and
  `Cors:AllowedOrigins` in the deployment environment. Production startup fails closed without
  database connectivity or an explicit allowed client origin. Startup provisions the Identity and
  reservation schemas before those endpoints are mapped.
- API failures use sanitized ProblemDetails with a correlation ID. Internal exception details are
  logged server-side only and are never returned to clients.

## Dependencies

New dependencies require human approval and a check of license, maintenance health, transitive
dependencies, and known vulnerabilities. V1 avoids paid SaaS, external identity, and external
reservation services.

Swashbuckle.AspNetCore 6.6.2 is approved for generated OpenAPI and Swagger UI documentation. The
`/swagger` UI and `/swagger/v1/swagger.json` document are registered only in Development; production
hosts do not expose documentation routes. The document describes the production bearer scheme
without embedding tokens or development member identifiers.

## Review lens

Security is a mandatory dimension of every independent review (see `prompts/review.md`), not a
separate afterthought phase.
