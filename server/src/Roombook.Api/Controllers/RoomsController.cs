using Microsoft.AspNetCore.Mvc;
using Roombook.Rooms;

namespace Roombook.Api.Controllers;

[ApiController]
[Route("api/rooms")]
public sealed class RoomsController(IRoomAvailability rooms) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoomResponse>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IReadOnlyList<RoomResponse>> List(CancellationToken cancellationToken)
    {
        var available = await rooms.ListActiveAsync(cancellationToken);
        return available.Select(room => new RoomResponse(
            room.Id.Value,
            room.Name,
            room.TimeZone.Id,
            room.WorkingPeriods.Select(period => new WorkingPeriodResponse(period.Start, period.End)).ToArray())).ToArray();
    }
}

public sealed record RoomResponse(Guid Id, string Name, string TimeZone, IReadOnlyList<WorkingPeriodResponse> WorkingPeriods);
public sealed record WorkingPeriodResponse(TimeOnly Start, TimeOnly End);
