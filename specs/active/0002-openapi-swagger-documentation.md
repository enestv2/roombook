# Spec 0002 — OpenAPI and Swagger documentation

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0002-plan.md`

## Intent

Developers and integrators need a reliable, browsable description of the Roombook HTTP API.
They should be able to discover available endpoints, request and response schemas, validation and
conflict errors, authentication requirements, and representative examples without reading the
implementation. Documentation should be enabled for local development while avoiding unnecessary
API exposure in production.

## Requirements

- The API publishes an OpenAPI document describing every public HTTP endpoint, its HTTP method,
  route, supported request content type, response status codes, and response content types.
- The document describes request and response schemas for room discovery, booking creation,
  successful bookings, conflict details and suggested slots, validation errors, authentication
  responses, and sanitized unexpected-error responses.
- Protected operations are visibly marked as requiring the production bearer authentication scheme;
  development authentication behavior is documented separately as a local-development aid.
- The document includes the API title, version, concise endpoint summaries, parameter descriptions,
  field requirements, UTC/offset timestamp format, 15-minute boundary rule, duration limits, and
  privacy behavior for conflict details.
- Swagger UI presents the generated document for interactive local exploration and includes useful
  operation grouping and example payloads/responses.
- OpenAPI JSON and Swagger UI are available in Development only; production requests to
  documentation routes do not expose the API description.
- Documentation generation does not change authorization, validation, conflict prevention, or
  error-handling behavior of the API.

## Constraints & out of scope

- Use the existing ASP.NET Core/.NET 10 stack and established dependency approval rules.
- Do not include secrets, real user data, internal database details, stack traces, or private
  implementation types in the document or examples.
- Out of scope: generating a separate client SDK, publishing documentation to an external site,
  redesigning existing endpoint contracts, and exposing Swagger UI in production.

## Acceptance criteria

- [ ] AC-1 — In Development, the OpenAPI JSON route responds successfully and describes every public API endpoint with method, route, and operation metadata.
- [ ] AC-2 — The document defines schemas and examples for room responses, booking requests and success responses, conflict responses with alternatives, field validation errors, authentication errors, and sanitized unexpected errors.
- [ ] AC-3 — Protected booking and authentication operations declare the production bearer security scheme, while the development-only authentication path is documented as local-only guidance.
- [ ] AC-4 — Timestamp fields are documented as offset-bearing/UTC-compatible values, and booking duration and 15-minute alignment constraints are visible in descriptions or schema metadata.
- [ ] AC-5 — Swagger UI loads in Development, groups operations coherently, and links to the generated OpenAPI document.
- [ ] AC-6 — In a non-Development environment, documentation routes are unavailable and no API schema is served.
- [ ] AC-7 — Existing booking behavior and authorization tests remain green, proving documentation wiring does not alter API behavior.
- [ ] AC-8 — The verification contract builds the server, runs documentation/API tests, and checks the client without requiring external services beyond existing configured test dependencies.

## Definition of Done

- [ ] Every acceptance criterion mapped to proof (test or reproducible observation)
- [ ] `scripts/check` green
- [ ] Independent review done; real findings fixed, noise rejected with written rationale
- [ ] Docs / ADRs updated if behavior or architecture changed
- [ ] Spec moved to `specs/done/` (it becomes immutable there)

## Scorecard (fill at ship — honest numbers make the process improvable)

| Metric | Value |
|---|---|
| Spec revisions | |
| Fix rounds | |
| Review findings: real / noise | |
| Regressions introduced | |
| Bugs escaped to production | |

## Self-critique

- The exact documentation route names are intentionally owned by the plan and implementation;
  recommendation: use the conventional `/swagger` UI and `/swagger/v1/swagger.json` document route
  to minimize surprise for .NET developers.
- The production bearer scheme is already part of the API contract; recommendation: document its
  header-based usage but never embed a real token in examples.
- The current API may not have XML documentation comments for every action; recommendation:
  use explicit operation summaries/descriptions and schema metadata for all public contracts, then
  add XML comments only where they improve generated detail.
