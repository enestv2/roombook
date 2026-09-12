using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Roombook.Api.Controllers;
using Roombook.Infrastructure;
using Roombook.Reservations;
using Roombook.Rooms;

namespace Roombook.Api.Tests;

public sealed class BookingApiTests
{
    [Fact]
    public async Task ConflictResponseOmitsConflictingMemberIdentity()
    {
        var roomId = new RoomId(Guid.NewGuid());
        var room = new Room(roomId, "Room", TimeZoneInfo.Utc, new[] { new WorkingPeriod(new TimeOnly(8, 0), new TimeOnly(18, 0)) });
        var repository = new InMemoryBookingRepository();
        var now = new DateTimeOffset(2030, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var service = new BookingService(new InMemoryRoomAvailability(new[] { room }), repository, new FixedTimeProvider(now));
        await service.CreateAsync(Guid.NewGuid(), new BookingCommand(roomId, now.AddDays(1).AddHours(1), now.AddDays(1).AddHours(2)));
        var controller = new BookingsController(service, new InMemoryRoomAvailability(new[] { room }))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal() } } };
        var result = await controller.Create(new CreateBookingRequest(roomId.Value, now.AddDays(1).AddHours(1), now.AddDays(1).AddHours(2)), default);
        var conflict = Assert.IsType<ObjectResult>(result);
        var body = Assert.IsType<ConflictResponse>(conflict.Value);
        Assert.DoesNotContain("MemberId", string.Join(',', body.GetType().GetProperties().Select(x => x.Name)));
    }

    [Fact]
    public async Task InvalidRoomReturnsFieldError()
    {
        var room = new Room(new RoomId(Guid.NewGuid()), "Room", TimeZoneInfo.Utc, new[] { new WorkingPeriod(new TimeOnly(8, 0), new TimeOnly(18, 0)) });
        var service = new BookingService(new InMemoryRoomAvailability(new[] { room }), new InMemoryBookingRepository(), new FixedTimeProvider(DateTimeOffset.UtcNow));
        var controller = new BookingsController(service, new InMemoryRoomAvailability(new[] { room }))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal() } } };
        var result = await controller.Create(new CreateBookingRequest(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(2)), default);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Contains("roomId", problem.Errors.Keys);
    }

    [Fact]
    public async Task TimezoneLessTimestampsReturnFieldErrors()
    {
        var room = new Room(new RoomId(Guid.NewGuid()), "Room", TimeZoneInfo.Utc,
            new[] { new WorkingPeriod(new TimeOnly(8, 0), new TimeOnly(18, 0)) });
        var service = new BookingService(new InMemoryRoomAvailability(new[] { room }),
            new InMemoryBookingRepository(), new FixedTimeProvider(DateTimeOffset.UtcNow));
        var controller = new BookingsController(service, new InMemoryRoomAvailability(new[] { room }))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal() } } };

        var result = await controller.Create(new CreateBookingRequest(room.Id.Value,
            "2030-01-01T09:00:00", "2030-01-01T10:00:00"), default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Contains("startsAt", problem.Errors.Keys);
        Assert.Contains("endsAt", problem.Errors.Keys);
    }

    [Fact]
    public async Task MissingAuthenticatedMemberIsRejected()
    {
        var room = new Room(new RoomId(Guid.NewGuid()), "Room", TimeZoneInfo.Utc,
            new[] { new WorkingPeriod(new TimeOnly(8, 0), new TimeOnly(18, 0)) });
        var service = new BookingService(new InMemoryRoomAvailability(new[] { room }),
            new InMemoryBookingRepository(), new FixedTimeProvider(DateTimeOffset.UtcNow));
        var controller = new BookingsController(service, new InMemoryRoomAvailability(new[] { room }))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var result = await controller.Create(new CreateBookingRequest(room.Id.Value,
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(1)), default);
        Assert.IsType<UnauthorizedResult>(result);
    }

    private static ClaimsPrincipal Principal() => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
        new Claim(ClaimTypes.Role, "Member")
    }, "test"));

    [Fact]
    public async Task NonMemberCannotCreateBooking()
    {
        var room = new Room(new RoomId(Guid.NewGuid()), "Room", TimeZoneInfo.Utc,
            new[] { new WorkingPeriod(new TimeOnly(8, 0), new TimeOnly(18, 0)) });
        var service = new BookingService(new InMemoryRoomAvailability(new[] { room }),
            new InMemoryBookingRepository(), new FixedTimeProvider(DateTimeOffset.UtcNow));
        var controller = new BookingsController(service, new InMemoryRoomAvailability(new[] { room }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
                    }, "test"))
                }
            }
        };
        var result = await controller.Create(new CreateBookingRequest(room.Id.Value,
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(1)), default);
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task RoomCatalogReturnsOnlyActiveConfiguredRooms()
    {
        var active = new Room(new RoomId(Guid.NewGuid()), "Active", TimeZoneInfo.Utc,
            new[] { new WorkingPeriod(new TimeOnly(8, 0), new TimeOnly(18, 0)) });
        var inactive = new Room(new RoomId(Guid.NewGuid()), "Inactive", TimeZoneInfo.Utc,
            new[] { new WorkingPeriod(new TimeOnly(8, 0), new TimeOnly(18, 0)) }, false);
        var controller = new RoomsController(new InMemoryRoomAvailability(new[] { inactive, active }));
        var result = await controller.List(default);
        var room = Assert.Single(result);
        Assert.Equal(active.Id.Value, room.Id);
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
