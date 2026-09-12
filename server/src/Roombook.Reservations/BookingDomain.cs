using Roombook.Rooms;

namespace Roombook.Reservations;

public readonly record struct BookingId(Guid Value)
{
    public static BookingId New() => new(Guid.NewGuid());
}

public sealed record Booking(
    BookingId Id,
    RoomId RoomId,
    Guid MemberId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    bool IsCancelled = false)
{
    public bool Overlaps(DateTimeOffset startUtc, DateTimeOffset endUtc) =>
        !IsCancelled && StartsAtUtc < endUtc && startUtc < EndsAtUtc;
}

public sealed record FieldError(string Field, string Code);
public sealed record BookingConflict(
    RoomId RoomId,
    DateTimeOffset RequestedStartUtc,
    DateTimeOffset RequestedEndUtc,
    DateTimeOffset ConflictingStartUtc,
    DateTimeOffset ConflictingEndUtc,
    IReadOnlyList<AlternativeSlot> Alternatives)
{
    public bool HasAlternatives => Alternatives.Count != 0;
}
public sealed record AlternativeSlot(DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc);

public sealed record BookingCommand(RoomId RoomId, DateTimeOffset StartsAt, DateTimeOffset EndsAt);

public interface IBookingRepository
{
    Task<IReadOnlyList<Booking>> FindOverlapsAsync(RoomId roomId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Booking>> ListForRoomAsync(RoomId roomId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);
    Task<Booking> AddIfNoOverlapAsync(Booking booking, CancellationToken cancellationToken = default);
}

public sealed class BookingValidator
{
    public IReadOnlyList<FieldError> Validate(BookingCommand command, Room room, DateTimeOffset nowUtc)
    {
        var errors = new List<FieldError>();
        if (!room.IsActive) errors.Add(new("roomId", "booking.room_unavailable"));
        var start = command.StartsAt.ToUniversalTime();
        var end = command.EndsAt.ToUniversalTime();
        if (start <= nowUtc) errors.Add(new("startsAt", "booking.start_in_past"));
        if (end <= start) errors.Add(new("endsAt", "booking.end_before_start"));
        if (start.TimeOfDay.Ticks % TimeSpan.FromMinutes(15).Ticks != 0)
            errors.Add(new("startsAt", "booking.start_not_aligned"));
        if (end.TimeOfDay.Ticks % TimeSpan.FromMinutes(15).Ticks != 0)
            errors.Add(new("endsAt", "booking.end_not_aligned"));
        var duration = end - start;
        if (duration < TimeSpan.FromMinutes(15)) errors.Add(new("endsAt", "booking.duration_too_short"));
        if (duration > TimeSpan.FromHours(4)) errors.Add(new("endsAt", "booking.duration_too_long"));
        if (!RoomSchedule.Contains(room, start, end)) errors.Add(new("time", "booking.outside_working_hours"));
        return errors;
    }
}

public sealed class AlternativeSlotFinder
{
    public async Task<IReadOnlyList<AlternativeSlot>> FindAsync(
        Room room, DateTimeOffset requestedStartUtc, TimeSpan duration, IBookingRepository repository,
        DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        var horizonEnd = nowUtc.AddDays(30);
        var existing = await repository.ListForRoomAsync(room.Id, nowUtc, horizonEnd, cancellationToken);
        return RoomSchedule.EnumerateSlots(room, requestedStartUtc, duration)
            .Where(x => x.StartUtc > nowUtc && x.StartUtc < horizonEnd)
            .Where(x => existing.All(b => !b.Overlaps(x.StartUtc, x.EndUtc)))
            .Select(x => new AlternativeSlot(x.StartUtc, x.EndUtc))
            .OrderBy(x => Math.Abs((x.StartsAtUtc - requestedStartUtc).Ticks))
            .ThenBy(x => x.StartsAtUtc)
            .Take(3)
            .ToArray();
    }
}

public interface IBookingResult;
public sealed record BookingSuccess(Booking Booking) : IBookingResult;
public sealed record BookingValidationFailure(IReadOnlyList<FieldError> Errors) : IBookingResult;
public sealed record BookingConflictFailure(BookingConflict Conflict) : IBookingResult;

public sealed class BookingService
{
    private readonly IRoomAvailability rooms;
    private readonly IBookingRepository bookings;
    private readonly BookingValidator validator;
    private readonly AlternativeSlotFinder alternatives;
    private readonly TimeProvider clock;

    public BookingService(IRoomAvailability rooms, IBookingRepository bookings, TimeProvider? clock = null,
        BookingValidator? validator = null, AlternativeSlotFinder? alternatives = null)
    {
        this.rooms = rooms; this.bookings = bookings; this.clock = clock ?? TimeProvider.System;
        this.validator = validator ?? new BookingValidator(); this.alternatives = alternatives ?? new AlternativeSlotFinder();
    }

    public async Task<IBookingResult> CreateAsync(
        Guid memberId, BookingCommand command, CancellationToken cancellationToken = default)
    {
        var room = await rooms.FindAsync(command.RoomId, cancellationToken);
        if (room is null) return new BookingValidationFailure(new[] { new FieldError("roomId", "booking.room_not_found") });
        var now = clock.GetUtcNow();
        var errors = validator.Validate(command, room, now);
        if (errors.Count != 0) return new BookingValidationFailure(errors);
        var start = command.StartsAt.ToUniversalTime();
        var end = command.EndsAt.ToUniversalTime();
        var conflicts = await bookings.FindOverlapsAsync(room.Id, start, end, cancellationToken);
        if (conflicts.Count != 0)
        {
            var suggestions = await alternatives.FindAsync(room, start, end - start, bookings, now, cancellationToken);
            var conflict = conflicts.OrderBy(x => x.StartsAtUtc).First();
            return new BookingConflictFailure(new BookingConflict(room.Id, start, end, conflict.StartsAtUtc, conflict.EndsAtUtc, suggestions));
        }
        var booking = new Booking(BookingId.New(), room.Id, memberId, start, end);
        try
        {
            return new BookingSuccess(await bookings.AddIfNoOverlapAsync(booking, cancellationToken));
        }
        catch (BookingOverlapException)
        {
            var conflict = (await bookings.FindOverlapsAsync(room.Id, start, end, cancellationToken)).OrderBy(x => x.StartsAtUtc).First();
            var suggestions = await alternatives.FindAsync(room, start, end - start, bookings, now, cancellationToken);
            return new BookingConflictFailure(new BookingConflict(room.Id, start, end, conflict.StartsAtUtc, conflict.EndsAtUtc, suggestions));
        }
    }
}

public sealed class BookingOverlapException : Exception
{
    public BookingOverlapException() : base("An active booking overlaps this interval.") { }
}
