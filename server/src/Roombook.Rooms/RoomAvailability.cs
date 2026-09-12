namespace Roombook.Rooms;

public readonly record struct RoomId(Guid Value)
{
    public static RoomId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public sealed record WorkingPeriod
{
    public TimeOnly Start { get; }
    public TimeOnly End { get; }

    public WorkingPeriod(TimeOnly start, TimeOnly end)
    {
        if (start >= end) throw new ArgumentException("Working period must end after it starts.");
        Start = start;
        End = end;
    }
}

public sealed record Room(
    RoomId Id,
    string Name,
    TimeZoneInfo TimeZone,
    IReadOnlyList<WorkingPeriod> WorkingPeriods,
    bool IsActive = true);

public interface IRoomAvailability
{
    ValueTask<Room?> FindAsync(RoomId id, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<Room>> ListActiveAsync(CancellationToken cancellationToken = default);
}

public sealed class InMemoryRoomAvailability : IRoomAvailability
{
    private readonly IReadOnlyDictionary<RoomId, Room> rooms;
    public InMemoryRoomAvailability(IEnumerable<Room> rooms) => this.rooms = rooms.ToDictionary(x => x.Id);
    public ValueTask<Room?> FindAsync(RoomId id, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(rooms.GetValueOrDefault(id));
    public ValueTask<IReadOnlyList<Room>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlyList<Room>>(rooms.Values.Where(x => x.IsActive).OrderBy(x => x.Name).ToArray());
}

public static class RoomSchedule
{
    public static bool Contains(Room room, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        if (endUtc <= startUtc) return false;
        var localStart = TimeZoneInfo.ConvertTime(startUtc, room.TimeZone);
        var localEnd = TimeZoneInfo.ConvertTime(endUtc, room.TimeZone);
        if (localStart.Date != localEnd.Date) return false;
        return room.WorkingPeriods.Any(period =>
            localStart.TimeOfDay >= period.Start.ToTimeSpan() &&
            localEnd.TimeOfDay <= period.End.ToTimeSpan());
    }

    public static IEnumerable<(DateTimeOffset StartUtc, DateTimeOffset EndUtc)> EnumerateSlots(
        Room room, DateTimeOffset requestedStartUtc, TimeSpan duration, int horizonDays = 30)
    {
        if (duration <= TimeSpan.Zero) yield break;
        var localStartDate = TimeZoneInfo.ConvertTime(requestedStartUtc, room.TimeZone).Date;
        var quarterTicks = TimeSpan.FromMinutes(15).Ticks;
        for (var offset = -horizonDays; offset <= horizonDays; offset++)
        {
            var date = localStartDate.AddDays(offset);
            foreach (var period in room.WorkingPeriods.OrderBy(p => p.Start))
            {
                var periodStart = date.Add(period.Start.ToTimeSpan());
                var startTicks = ((periodStart.TimeOfDay.Ticks + quarterTicks - 1) / quarterTicks) * quarterTicks;
                var start = date.AddTicks(startTicks);
                var endOfPeriod = date.Add(period.End.ToTimeSpan());
                for (var candidate = start; candidate + duration <= endOfPeriod; candidate = candidate.AddMinutes(15))
                {
                    if (!TryConvertLocalToUtc(candidate, room.TimeZone, out var startUtc) ||
                        !TryConvertLocalToUtc(candidate + duration, room.TimeZone, out var endUtc))
                        continue;
                    if (endUtc - startUtc != duration)
                        continue;
                    yield return (startUtc, endUtc);
                }
            }
        }
    }

    private static bool TryConvertLocalToUtc(DateTime local, TimeZoneInfo timeZone, out DateTimeOffset utc)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local))
        {
            utc = default;
            return false;
        }

        var offset = timeZone.IsAmbiguousTime(local)
            ? timeZone.GetAmbiguousTimeOffsets(local).Min()
            : timeZone.GetUtcOffset(local);
        utc = new DateTimeOffset(local, offset).ToUniversalTime();
        return true;
    }
}
