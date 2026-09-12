using Roombook.Infrastructure;
using Roombook.Reservations;
using Roombook.Rooms;

namespace Roombook.Reservations.Tests;

public sealed class ReservationTests
{
    private static readonly Guid Member = Guid.NewGuid();
    private static readonly RoomId RoomId = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 8, 0, 0, TimeSpan.Zero);

    private static (BookingService Service, InMemoryBookingRepository Repository) Create()
    {
        var room = new Room(RoomId, "Board room", TimeZoneInfo.Utc,
            new[] { new WorkingPeriod(new TimeOnly(8, 0), new TimeOnly(18, 0)) });
        var repository = new InMemoryBookingRepository();
        return (new BookingService(new InMemoryRoomAvailability(new[] { room }), repository, new FixedTimeProvider(Now)), repository);
    }

    [Fact]
    public async Task CreatesBookingForAvailableFutureInterval()
    {
        var (service, _) = Create();
        var result = await service.CreateAsync(Member, new BookingCommand(RoomId, At(9), At(10)));
        var success = Assert.IsType<BookingSuccess>(result);
        Assert.Equal(Member, success.Booking.MemberId);
        Assert.Equal(At(9), success.Booking.StartsAtUtc);
    }

    [Fact]
    public async Task RejectsUnalignedBoundary()
    {
        var (service, repository) = Create();
        var result = await service.CreateAsync(Member, new BookingCommand(RoomId, At(9).AddMinutes(1), At(10)));
        var failure = Assert.IsType<BookingValidationFailure>(result);
        Assert.Contains(failure.Errors, x => x.Field == "startsAt");
        Assert.Empty(await repository.ListForRoomAsync(RoomId, Now, Now.AddDays(2)));
    }

    [Fact]
    public async Task RejectsFractionalTickBoundary()
    {
        var (service, repository) = Create();
        var result = await service.CreateAsync(Member, new BookingCommand(RoomId, At(9).AddTicks(1), At(10)));

        var failure = Assert.IsType<BookingValidationFailure>(result);
        Assert.Contains(failure.Errors, x => x.Field == "startsAt");
        Assert.Empty(await repository.ListForRoomAsync(RoomId, Now, Now.AddDays(2)));
    }

    [Fact]
    public async Task RejectsDurationOutsideLimits()
    {
        var (service, _) = Create();
        var shortResult = await service.CreateAsync(Member, new BookingCommand(RoomId, At(9), At(9).AddMinutes(10)));
        var longResult = await service.CreateAsync(Member, new BookingCommand(RoomId, At(9), At(14)));
        Assert.IsType<BookingValidationFailure>(shortResult);
        Assert.IsType<BookingValidationFailure>(longResult);
    }

    [Fact]
    public async Task RejectsPastAndOutsideWorkingHours()
    {
        var (service, _) = Create();
        var past = await service.CreateAsync(Member, new BookingCommand(RoomId, At(7), At(8)));
        var outside = await service.CreateAsync(Member, new BookingCommand(RoomId, At(18), At(19)));
        Assert.IsType<BookingValidationFailure>(past);
        Assert.IsType<BookingValidationFailure>(outside);
    }

    [Fact]
    public async Task RejectsOverlapWithoutCreatingBooking()
    {
        var (service, repository) = Create();
        Assert.IsType<BookingSuccess>(await service.CreateAsync(Member, new BookingCommand(RoomId, At(9), At(10))));
        var result = await service.CreateAsync(Guid.NewGuid(), new BookingCommand(RoomId, At(9).AddMinutes(15), At(10).AddMinutes(15)));
        var conflict = Assert.IsType<BookingConflictFailure>(result);
        Assert.Equal(At(9), conflict.Conflict.ConflictingStartUtc);
        Assert.DoesNotContain(conflict.Conflict.Alternatives, x => x.StartsAtUtc == At(9).AddMinutes(15));
        Assert.Single(await repository.ListForRoomAsync(RoomId, Now, Now.AddDays(2)));
    }

    [Fact]
    public async Task AlternativesAreSameDurationOrderedAndLimitedToThree()
    {
        var (service, _) = Create();
        Assert.IsType<BookingSuccess>(await service.CreateAsync(Member, new BookingCommand(RoomId, At(9), At(10))));
        var result = await service.CreateAsync(Member, new BookingCommand(RoomId, At(9), At(10)));
        var conflict = Assert.IsType<BookingConflictFailure>(result).Conflict;
        Assert.Equal(3, conflict.Alternatives.Count);
        Assert.All(conflict.Alternatives, x => Assert.Equal(TimeSpan.FromHours(1), x.EndsAtUtc - x.StartsAtUtc));
        Assert.True(conflict.Alternatives.Zip(conflict.Alternatives.Skip(1),
            (a, b) => Math.Abs((a.StartsAtUtc - At(9)).Ticks) <= Math.Abs((b.StartsAtUtc - At(9)).Ticks)).All(x => x));
    }

    [Fact]
    public async Task AlternativesIncludeEarlierFutureWorkingPeriods()
    {
        var (service, _) = Create();
        Assert.IsType<BookingSuccess>(await service.CreateAsync(Member, new BookingCommand(RoomId, At(9), At(10))));
        var requestedStart = new DateTimeOffset(2030, 1, 20, 9, 0, 0, TimeSpan.Zero);
        var requestedEnd = new DateTimeOffset(2030, 1, 20, 10, 0, 0, TimeSpan.Zero);
        Assert.IsType<BookingSuccess>(await service.CreateAsync(Member, new BookingCommand(RoomId, requestedStart, requestedEnd)));
        var result = await service.CreateAsync(Member, new BookingCommand(RoomId,
            requestedStart, requestedEnd));
        var conflict = Assert.IsType<BookingConflictFailure>(result).Conflict;
        Assert.Contains(conflict.Alternatives, x => x.StartsAtUtc < requestedStart && x.StartsAtUtc > Now);
    }

    [Fact]
    public async Task ConcurrentRequestsAllowAtMostOneOverlappingCommit()
    {
        var (service, repository) = Create();
        var results = await Task.WhenAll(
            service.CreateAsync(Member, new BookingCommand(RoomId, At(11), At(12))),
            service.CreateAsync(Guid.NewGuid(), new BookingCommand(RoomId, At(11), At(12))));
        Assert.Single(results.OfType<BookingSuccess>());
        Assert.Single(await repository.ListForRoomAsync(RoomId, Now, Now.AddDays(2)));
    }

    [Fact]
    public async Task ReportsNoAlternativesWhenRoomIsFullyUnavailable()
    {
        var room = new Room(RoomId, "Small room", TimeZoneInfo.Utc,
            new[] { new WorkingPeriod(new TimeOnly(8, 0), new TimeOnly(10, 0)) });
        var repository = new InMemoryBookingRepository();
        var service = new BookingService(new InMemoryRoomAvailability(new[] { room }), repository, new FixedTimeProvider(Now));
        for (var day = 0; day <= 30; day++)
        {
            var start = Now.UtcDateTime.Date.AddDays(day).AddHours(8);
            await repository.AddIfNoOverlapAsync(new Booking(BookingId.New(), RoomId, Member,
                new DateTimeOffset(start, TimeSpan.Zero), new DateTimeOffset(start.AddHours(2), TimeSpan.Zero)));
        }
        var result = await service.CreateAsync(Member, new BookingCommand(RoomId, At(9), At(10)));
        var conflict = Assert.IsType<BookingConflictFailure>(result).Conflict;
        Assert.Empty(conflict.Alternatives);
    }

    [Fact]
    public async Task NormalizesOffsetInputToUtc()
    {
        var room = new Room(RoomId, "Local room", TimeZoneInfo.CreateCustomTimeZone("UTC+03", TimeSpan.FromHours(3), "UTC+03", "UTC+03"),
            new[] { new WorkingPeriod(new TimeOnly(12, 0), new TimeOnly(14, 0)) });
        var service = new BookingService(new InMemoryRoomAvailability(new[] { room }),
            new InMemoryBookingRepository(), new FixedTimeProvider(Now));
        var result = await service.CreateAsync(Member, new BookingCommand(RoomId,
            new DateTimeOffset(2030, 1, 11, 12, 0, 0, TimeSpan.FromHours(3)),
            new DateTimeOffset(2030, 1, 11, 13, 0, 0, TimeSpan.FromHours(3))));
        var booking = Assert.IsType<BookingSuccess>(result).Booking;
        Assert.Equal(new DateTimeOffset(2030, 1, 11, 9, 0, 0, TimeSpan.Zero), booking.StartsAtUtc);
    }

    [Fact]
    public void SkipsInvalidAndUsesDeterministicAmbiguousDstSlots()
    {
        var daylightRule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2000, 1, 1), new DateTime(2100, 12, 31), TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 10),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 3));
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test DST", TimeSpan.Zero, "Test DST", "Test DST",
            "Test DST", new[] { daylightRule });
        var room = new Room(RoomId, "DST room", zone,
            new[] { new WorkingPeriod(new TimeOnly(1, 0), new TimeOnly(4, 0)) });
        var springSlots = RoomSchedule.EnumerateSlots(room,
            new DateTimeOffset(2030, 3, 10, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1), 0).ToArray();
        Assert.DoesNotContain(springSlots, slot => TimeZoneInfo.ConvertTime(slot.StartUtc, zone).Hour == 2);

        var fallSlots = RoomSchedule.EnumerateSlots(room,
            new DateTimeOffset(2030, 11, 3, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1), 0).ToArray();
        Assert.Equal(fallSlots.Length, fallSlots.Select(slot => slot.StartUtc).Distinct().Count());
        Assert.Contains(fallSlots, slot => slot.StartUtc == new DateTimeOffset(2030, 11, 3, 1, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void AlternativesUseExactQuarterBoundariesAndUtcDurationAcrossDst()
    {
        var room = new Room(RoomId, "Precise room", TimeZoneInfo.Utc,
            new[] { new WorkingPeriod(new TimeOnly(8, 0).Add(TimeSpan.FromTicks(1)), new TimeOnly(10, 0)) });
        var slots = RoomSchedule.EnumerateSlots(room,
            new DateTimeOffset(2030, 1, 11, 9, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1), 0).ToArray();
        Assert.Equal(new DateTimeOffset(2030, 1, 11, 8, 15, 0, TimeSpan.Zero), slots[0].StartUtc);
        Assert.All(slots, slot => Assert.Equal(TimeSpan.FromHours(1), slot.EndUtc - slot.StartUtc));

        var daylightRule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2000, 1, 1), new DateTime(2100, 12, 31), TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 10),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 3));
        var zone = TimeZoneInfo.CreateCustomTimeZone("Precise DST", TimeSpan.Zero, "Precise DST", "Precise DST",
            "Precise DST", new[] { daylightRule });
        var dstRoom = new Room(RoomId, "DST room", zone,
            new[] { new WorkingPeriod(new TimeOnly(1, 0), new TimeOnly(4, 0)) });
        var dstSlots = RoomSchedule.EnumerateSlots(dstRoom,
            new DateTimeOffset(2030, 3, 10, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(2), 0).ToArray();
        Assert.All(dstSlots, slot => Assert.Equal(TimeSpan.FromHours(2), slot.EndUtc - slot.StartUtc));
    }

    private static DateTimeOffset At(int hour) => new(2030, 1, 11, hour, 0, 0, TimeSpan.Zero);
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
