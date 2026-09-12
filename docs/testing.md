# Testing

## The contract

- Every acceptance criterion maps to at least one test (criterion <-> test map lives in the plan).
- Tests assert behavior, not implementation details or mere status codes.
- The whole suite runs inside `scripts/check` -- one command, everywhere.

## Frameworks & layout

Use xUnit (or the repository's selected .NET test framework) for backend unit and integration
tests, and the React project's configured component test framework for frontend tests. Keep tests
near their owning project or in the corresponding test project. Use an API/E2E suite for the
critical search-to-reservation flow.

## What must be tested

- Every rule BR-1 through BR-8, including boundaries, empty availability, and invalid input.
- Authorization for member versus administrator operations.
- Transactional prevention of overlapping reservations.
- UTC/local-time conversion and cancellation cutoff behavior.
- At least one end-to-end reservation flow from availability search to confirmation.
- Forbidden dependency and secret-handling checks where tooling supports them.

## Protected-tests rule

Weakening asserts, deleting, or skipping tests to reach green is forbidden. A red test triggers
`prompts/recovery/red-test.md` (R-02) -- first decide what is wrong: code, test, or spec.

## Determinism

Flaky tests are fixed, not retried or skipped -- see R-03. Evidence of a fix: 5 consecutive green runs.
