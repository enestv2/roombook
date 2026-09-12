# Spec 0004 — Professional frontend workspace redesign

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0004-plan.md`

## Intent

RoomBook needs a production-ready frontend experience that feels trustworthy and clear while a member books a room. The redesign will make the existing room-loading and booking flow easier to scan, responsive on desktop, tablet, and mobile, and consistent across loading, empty, success, conflict, and error states. The existing Turkish/English experience and UTC/ISO API contract remain intact. This feature deliberately does not invent dashboard, booking-history, room-management, or schedule data that the current API does not provide.

## Requirements

- Present the existing booking workflow inside a coherent SaaS-style application shell with a clear brand area, page header, content hierarchy, and responsive layout.
- Provide a branded RoomBook sidebar that is expanded by default on desktop, collapsible to an icon rail, and available as a keyboard-accessible mobile drawer with open/close controls.
- Keep language switching available from the interface; the control displays `TR` and `EN`, while accessible labels remain localized.
- Use the rooms returned by the existing API as the only source for room identity, name, timezone, and working-period information. When multiple rooms are returned, the member can choose the room before booking; when no rooms are returned, show a purposeful empty state.
- Preserve the existing booking behavior: start and end date-time inputs are interpreted in the selected room timezone, requests continue to use UTC/ISO values, successful bookings show the server confirmation, and conflict alternatives remain selectable.
- Show clear visual states for room loading, room-load failure with retry, booking submission in progress, field/form errors, authorization errors, booking conflicts, no conflict alternatives, and booking success.
- Display API-provided localized messages when available and use the selected interface language for client-side fallback messages and labels.
- Establish reusable frontend presentation primitives for the implemented flow, including buttons, form fields, cards, badges/status indicators, page/section headers, empty state, skeleton loading state, and feedback messaging where appropriate.
- Keep interactive elements keyboard accessible, associate labels and errors with controls, expose status updates to assistive technology, and maintain usable focus/contrast states.
- Keep the interface usable at desktop, tablet, and mobile widths without horizontal scrolling or loss of the booking action.
- Keep backend code, API routes, request/response shapes, authentication behavior, business rules, and existing test intent unchanged.

## Constraints & out of scope

- No backend changes, new API endpoints, mock records, fabricated metrics, or fake navigation destinations.
- No booking-history, calendar/schedule, room-administration, member-profile, or dashboard screens unless they can be backed by existing real data and routes; the current scope is the available booking workflow.
- Do not change the UTC/ISO wire format or the room-timezone conversion rules.
- Do not weaken, remove, or skip existing tests. New UI behavior must be covered by focused frontend tests and the repository verification command.
- Tailwind CSS is the styling foundation, with the smallest reasonable dependency/configuration change for the existing Vite frontend.

## Acceptance criteria

- [x] AC-1 — The existing booking flow is rendered in a coherent responsive application shell with a visible page title, current workflow context, language control, and no horizontal scrolling at desktop, tablet, or mobile viewport sizes.
- [x] AC-2 — The language control visibly offers only `TR` and `EN`, retains localized accessible names, and changing language updates visible UI copy without clearing entered date-time values.
- [x] AC-3 — While rooms are loading, the interface shows a non-jarring skeleton state; when the API returns no rooms, it shows an accessible empty state; when loading fails, it shows a localized error and a retry action.
- [x] AC-4 — Room data displayed to the member comes only from `GET /api/rooms`; each selectable room shows its returned name and timezone, and selecting a room makes subsequent booking requests use that room ID and timezone.
- [x] AC-5 — The booking form preserves room-timezone conversion and sends the existing booking payload with UTC/ISO `startsAt` and `endsAt` values; no API contract changes are introduced.
- [x] AC-6 — During booking submission the primary action communicates progress and prevents duplicate submission; server field/form errors, authorization errors, and client fallback errors are visible, localized where the API does not provide a localized message, and associated with the relevant form context.
- [x] AC-7 — A successful booking shows the existing server confirmation in an accessible success state; a conflict shows the conflict message and renders each returned alternative as an actionable choice that updates the form values without inventing alternatives.
- [x] AC-8 — Reusable UI primitives used by the flow have consistent visual states for default, hover, focus-visible, disabled, loading, error, empty, and success where applicable.
- [x] AC-9 — Existing frontend tests remain green, new tests cover the shell/room states, language control, room selection, submission state, and conflict alternative interaction, and `scripts/check` passes.
- [x] AC-10 — The final diff contains no backend changes, no mock data, no weakened assertions, and only the approved frontend/spec/plan/test/documentation changes.
- [x] AC-11 — The sidebar shows the RoomBook logo, collapses to an icon rail on desktop, opens from a mobile menu control, and can be closed with its close control, backdrop, or Escape without obscuring the booking workflow.
- [x] AC-12 — The desktop sidebar occupies the full viewport height and remains stationary while only the content region scrolls; its fixed width and overflow rules prevent Turkish or English copy from resizing or breaking the sidebar.

## Verification evidence

- AC-1, AC-3, and AC-8: final production build smoke-reviewed at desktop, tablet, and narrow viewport sizes; responsive hardening received independent read-only approval.
- AC-2 and AC-4: `client/src/App.test.tsx`, `client/src/LanguageSelector.test.tsx`.
- AC-5: `client/src/api.test.ts`, `client/src/BookingForm.test.tsx`.
- AC-6 and AC-7: `client/src/BookingForm.test.tsx`, `client/src/api.test.ts`.
- AC-9: `scripts/check` completed with `CHECK GREEN (3 steps)`; frontend suite passed 24/24 tests.
- AC-10: independent read-only review found no backend/API-route changes, mock data, weakened assertions, or scope drift.
- AC-11: `client/src/App.test.tsx` verifies desktop collapse/expand, mobile open, close-button, backdrop, Escape, focus trap, and focus restoration behavior; independent re-review approved the implementation.
- AC-12: `AppShell` uses a fixed-height overflow-hidden shell, a full-height desktop sidebar, an independently scrollable content region, and fixed-width/min-width/overflow text rules for both locales.

## Definition of Done

- [x] Every acceptance criterion mapped to proof (test or reproducible observation)
- [x] `scripts/check` green
- [x] Independent review done; real findings fixed, noise rejected with written rationale
- [x] Docs / ADRs updated if behavior or architecture changed
- [ ] Spec moved to `specs/done/` (it becomes immutable there)

## Scorecard (fill at ship — honest numbers make the process improvable)

| Metric | Value |
|---|---|
| Spec revisions | 1 |
| Fix rounds | 4 |
| Review findings: real / noise | 4 / 0 |
| Regressions introduced | 0 |
| Bugs escaped to production | 0 |
