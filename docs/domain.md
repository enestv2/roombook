# Domain

## Ubiquitous language

| Term | Meaning | Notes / not to be confused with |
|---|---|---|
| User | A person who can use Roombook | Has the Member or Administrator role |
| Member | A user who manages their own reservations | Cannot manage rooms or other users' reservations |
| Administrator | A user who manages rooms, schedules, and all reservations | Elevated role; default deny |
| Room | A reservable meeting or work space | Has availability and descriptive attributes |
| Reservation | A time-bounded claim on one room by one member | Cannot overlap another active reservation |
| Working hours | The periods in which a room may be reserved | Stored/displayed with explicit timezone handling |
| Cancellation | Ending a future reservation before its start | Allowed until 15 minutes before start |
| Conflict response | Privacy-safe response when a requested interval overlaps an active reservation | Includes the room, requested/conflicting intervals, and up to three alternatives; never includes the other member |
| Alternative slot | A free interval with the requested duration | 15-minute aligned, within working hours, next 30 calendar days, nearest first with earlier ties first |

## Business rules

1. **BR-1:** Reservation times align to 15-minute boundaries.
2. **BR-2:** A reservation lasts at least 15 minutes and at most 4 hours.
3. **BR-3:** A reservation is future-dated and lies within the room's working hours.
4. **BR-4:** Active reservations for the same room must not overlap.
5. **BR-5:** A member may create and cancel their own reservations; an administrator may manage all reservations.
6. **BR-6:** Cancellation is allowed only until 15 minutes before the reservation starts.
7. **BR-7:** Past reservations cannot be changed.
8. **BR-8:** Timestamps are stored in UTC and displayed in the relevant user/room timezone.

## Key domain invariants

- A room cannot have two overlapping active reservations.
- Authorization cannot be bypassed by changing a client-supplied user or role value.
- A past reservation is immutable.
