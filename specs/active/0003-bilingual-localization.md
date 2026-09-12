# Spec 0003 — Turkish and English localization

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0003-plan.md`

## Intent

Members should be able to use Roombook in Turkish or English and choose the language that is
most comfortable for them. The selected language should apply consistently to the interface and
to messages returned by the service, especially validation, authorization, conflict, and
unexpected-error messages. A first-time visitor should receive a sensible language without
having to configure the application before making a reservation. This feature does not add
additional supported languages or translate developer documentation.

## Requirements

- A member can choose Turkish or English from a visible language selector whose visual options are
  the language codes `TR` and `EN`.
- The application uses the member's saved language choice on later visits.
- On a first visit, the application selects Turkish when the browser prefers Turkish and English
  otherwise.
- Unsupported or malformed language preferences fall back to English.
- All user-visible application text, including navigation, room booking, confirmation, conflict,
  loading, empty, authentication, and failure messages, is available in Turkish and English.
- Changing the language updates interface text without a full page reload and preserves the
  member's current booking form values.
- New service requests use the currently selected language for returned user-facing messages.
- Service error responses provide localized titles/details/messages and stable machine-readable
  error codes that do not change when the language changes.
- Field validation errors preserve field-level guidance in the selected language and provide
  stable field error codes without exposing implementation details.
- Conflict responses provide their message in the selected language without revealing the other
  member's identity.
- Authentication, authorization, validation, not-found, and unexpected service errors use the
  selected language; unexpected errors remain generic and correlation-friendly.
- Existing consumers that read field errors as arrays of messages continue to receive that shape;
  machine-readable error codes are provided in addition to those messages.
- A language change does not re-request already displayed service errors; those messages remain
  as returned until the relevant state is replaced by a later action.

## Constraints & out of scope

- Supported languages are exactly Turkish (`tr`) and English (`en`) for this feature.
- English is the fallback when no language is selected, the preference is unsupported, or a
  translation is missing.
- Language preference input is treated as untrusted and is matched only against the supported
  language allowlist.
- Error messages must not contain stack traces, SQL, secrets, internal type names, or sensitive
  account details.
- The language selector must be keyboard accessible and have an accessible label.
- New localization dependencies require the approved `i18next`/`react-i18next` approach; no
  external translation service is introduced.
- Out of scope: additional languages, translating Swagger/OpenAPI descriptions, server-side
  translation management, automatic translation, and changing unrelated business rules or API
  authorization behavior.

## Acceptance criteria

- [ ] AC-1 — A visitor can select `TR` or `EN` from a visible, keyboard-accessible language
  selector; each option has an accessible label identifying the full language name.
- [ ] AC-2 — Selecting a language updates all currently rendered application-owned interface text
  without a full page reload and preserves the current booking form values.
- [ ] AC-3 — The selected language is restored on a later visit; on a first visit Turkish is chosen
  for a Turkish-preferred browser and English otherwise.
- [ ] AC-4 — Missing, malformed, or unsupported language preferences resolve to English and never
  cause an application error.
- [ ] AC-5 — A new service request includes the selected language preference, and the service uses
  it to localize user-facing error titles, details, validation messages, authorization messages,
  and booking conflict messages.
- [ ] AC-6 — Every supported-language service error response includes a stable top-level error
  code; changing between Turkish and English changes human-readable text but not the code.
- [ ] AC-7 — Validation responses retain the existing field-to-array-of-message shape and also
  provide stable field error codes for each returned field error.
- [ ] AC-8 — Turkish and English responses exist for booking validation, unauthorized access,
  forbidden access, booking conflicts, and sanitized unexpected failures.
- [ ] AC-9 — Conflict responses remain privacy-safe and contain no other member's identity in
  either language.
- [ ] AC-10 — Changing the selected language does not re-request or silently replace an already
  displayed service error; a later request returns the newly selected language.
- [ ] AC-11 — Existing booking, authentication, authorization, room, timezone, and conflict
  behavior remains green under the verification contract.
- [ ] AC-12 — No supported-language response exposes stack traces, SQL, secrets, internal type
  names, or sensitive account details.

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

- The exact stable error-code catalog is not yet enumerated. Recommendation: define one code per
  observable error category and one code per field-level validation rule in the plan, then test
  the catalog so translations cannot accidentally change it.
- The exact browser preference precedence when a saved choice is absent is described as browser
  language first and English fallback, but the implementation must normalize regional tags such
  as `tr-TR` to `tr`. Recommendation: make supported-language matching explicit and deterministic.
- The exact response property name for the additional field error-code map is not fixed beyond
  the required behavior. Recommendation: use `errorCodes` parallel to the existing `errors` map
  to preserve current consumers.
- The built-in identity endpoints may produce framework-generated errors with a different shape.
  Recommendation: verify and normalize those responses in the plan rather than silently leaving
  authentication errors untranslated.
- The current client may have user-visible text outside the inspected booking flow. Recommendation:
  inventory all rendered strings during planning and add a test or reproducible observation for
  each screen.
