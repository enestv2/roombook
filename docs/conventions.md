# Conventions

## Language & framework versions

- Backend: ASP.NET Core 10 / .NET 10.
- Frontend: React with TypeScript.
- Database: PostgreSQL.

## Naming

Use the established conventions of C#, ASP.NET Core, TypeScript, and React. Name domain concepts
consistently with `docs/domain.md`; test names describe observable behavior and the rule they cover.

## Error handling

Validate at API and domain boundaries. Return structured field-level validation errors for invalid
input and a generic correlation-friendly message for unexpected failures. Never expose stack
traces, SQL, secrets, or internal identifiers to users; log unexpected failures centrally.

## Data rules

Store timestamps in UTC and convert only at presentation boundaries. Use database transactions for
reservation conflict checks. Treat client-supplied role, owner, and availability data as untrusted.
Do not use floating point for values that require exactness.

## Enforced by tooling

`scripts/check` is the single contract. Once the application exists it runs .NET build/tests with
warnings as errors and the React lint/typecheck/test/production-build commands; architectural and
security rules remain reviewable until automated checks are added.
