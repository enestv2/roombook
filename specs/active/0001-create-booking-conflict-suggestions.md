# Spec 0001 — Create booking with conflict suggestions

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0001-plan.md`

## Intent

A member wants to reserve a room for a future meeting without accidentally colliding with
another reservation. A successful request creates a booking for the requested room and time and
confirms the details. When the time is unavailable, the member receives enough conflict detail to
understand why it failed and useful nearby times to try instead. This feature does not cover
recurring bookings, notifications, or calendar integrations.

## Requirements

- An authorized member can create a booking for a room by providing a start and end time.
- A booking must be future-dated, use 15-minute boundaries, last at least 15 minutes and no more
  than 4 hours, and fit within the room's working hours.
- A booking start must be strictly later than the current instant.
- A booking succeeds only when the requested room has no overlapping active booking for the
  requested interval.
- A successful booking is associated with the requesting member and room and includes the
  confirmed start and end times.
- A request that overlaps an active booking is rejected without creating a booking.
- A conflict response identifies the room and requested interval, gives the conflicting booking's
  start and end times, and does not reveal the other member's identity.
- A conflict response suggests up to three free intervals with the same duration as requested.
  Suggestions use 15-minute boundaries, fit within working hours, and are ordered by the smallest
  distance from the requested start time. The search considers both earlier and later intervals
  across future working periods for up to 30 calendar days. Equally near intervals are ordered
  earlier first, then later.
- If no suitable alternatives exist, the conflict response clearly states that no alternatives
  were found.
- Invalid room, time, or booking data is rejected with field-level guidance and does not create a
  booking.
- An unauthenticated user cannot create a booking. A member may create only their own booking;
  client-supplied identity or role values cannot change the acting user.

## Constraints & out of scope

- Timestamps must be interpreted with explicit timezone handling and stored in UTC; displayed
  times use the relevant user or room timezone.
- Conflict prevention must remain correct when simultaneous requests target the same room and
  interval.
- Out of scope: editing or cancelling bookings, recurring bookings, room administration, payments,
  notifications, external calendar or identity integrations, and choosing a different duration in
  conflict suggestions.

## Acceptance criteria

- [ ] AC-1 — An authenticated member can submit a valid future interval for an available room and receives a confirmed booking containing the room, member, start, and end times.
- [ ] AC-2 — A booking request is rejected when its start or end is not on a 15-minute boundary.
- [ ] AC-3 — A booking request is rejected when its duration is shorter than 15 minutes or longer than 4 hours.
- [ ] AC-4 — A booking request is rejected when it is not future-dated or falls outside the room's working hours.
- [ ] AC-5 — A booking request that overlaps an active booking for the same room is rejected and creates no new booking.
- [ ] AC-6 — A conflict response includes the room, requested interval, and conflicting interval, but not the conflicting member's identity.
- [ ] AC-7 — A conflict response returns no more than three free alternatives, each with the requested duration, 15-minute boundaries, and room working-hours compliance.
- [ ] AC-8 — Returned alternatives are ordered by proximity to the requested start, use earlier-before-later tie-breaking, may include earlier or later future working periods, and are limited to the next 30 calendar days.
- [ ] AC-9 — When no suitable alternative exists, the response explicitly reports that no alternatives were found.
- [ ] AC-10 — Invalid room or time input produces field-level guidance and does not create a booking.
- [ ] AC-11 — An unauthenticated user cannot create a booking, and changing client-supplied identity or role data cannot create it for another member.
- [ ] AC-12 — Concurrent requests for overlapping intervals in the same room result in at most one successful booking.
- [ ] AC-13 — Stored booking timestamps remain UTC while confirmation and conflict details are displayed in the relevant timezone.

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

- The exact shape and transport status of validation and conflict errors is intentionally left to the
  plan and implementation, while the observable content is defined above.
- A booking start is strictly later than the current instant; a booking starting exactly now is
  rejected because it cannot reliably be completed before it begins.
- Equally near intervals are ordered by earlier start time first, then later start time, making
  results deterministic.
- The search horizon for alternatives is not bounded. Recommendation: search through the next
- Alternatives are searched through the next 30 calendar days; results beyond that horizon are not
  considered, preventing unbounded work while keeping suggestions useful.
