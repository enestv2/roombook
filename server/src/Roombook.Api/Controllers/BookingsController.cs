using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using Roombook.Api.Localization;
using Roombook.Reservations;
using Roombook.Rooms;

namespace Roombook.Api.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize(Roles = "Member")]
public sealed class BookingsController(
    BookingService service,
    IRoomAvailability rooms,
    IStringLocalizer<ApiMessages> messages) : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType(typeof(ConflictResponse), StatusCodes.Status409Conflict, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> Create(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseExplicitTimestamp(request.StartsAt, out var startsAt))
        {
            ModelState.AddModelError("startsAt", messages[ApiErrorCodes.TimestampOffsetRequired].Value);
            LocalizedProblemDetails.AddFieldCode(HttpContext, "startsAt", ApiErrorCodes.TimestampOffsetRequired);
        }
        if (!TryParseExplicitTimestamp(request.EndsAt, out var endsAt))
        {
            ModelState.AddModelError("endsAt", messages[ApiErrorCodes.TimestampOffsetRequired].Value);
            LocalizedProblemDetails.AddFieldCode(HttpContext, "endsAt", ApiErrorCodes.TimestampOffsetRequired);
        }
        if (!ModelState.IsValid)
        {
            return BadRequest(CreateValidationProblem(ModelState));
        }
        var memberClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (memberClaim is null || !Guid.TryParse(memberClaim.Value, out var memberId))
            return Unauthorized();
        if (!User.IsInRole("Member")) return Forbid();

        var result = await service.CreateAsync(memberId, new BookingCommand(
            new RoomId(request.RoomId), startsAt.ToUniversalTime(), endsAt.ToUniversalTime()), cancellationToken);
        return result switch
        {
            BookingSuccess success => CreatedAtAction(nameof(Get), new { id = success.Booking.Id.Value }, await ToResponse(success.Booking)),
            BookingValidationFailure failure => Validation(failure.Errors),
            BookingConflictFailure conflict => await Conflict(conflict.Conflict),
            _ => Problem("The booking could not be created.")
        };
    }

    private static bool TryParseExplicitTimestamp(string? value, out DateTimeOffset timestamp)
    {
        timestamp = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var trimmed = value.Trim();
        var hasExplicitOffset = trimmed.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
            || System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"[+-]\d{2}:\d{2}$");
        return hasExplicitOffset
            && DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public IActionResult Get(Guid id) => NotFound();

    private IActionResult Validation(IReadOnlyList<FieldError> errors)
    {
        var fieldErrors = errors.GroupBy(x => x.Field)
            .ToDictionary(x => x.Key, x => x.Select(v => messages[v.Code].Value).ToArray());
        var errorCodes = errors.GroupBy(x => x.Field)
            .ToDictionary(x => x.Key, x => x.Select(v => v.Code).ToArray());
        return BadRequest(LocalizedProblemDetails.FromErrors(HttpContext, fieldErrors, errorCodes, messages));
    }

    private ValidationProblemDetails CreateValidationProblem(
        ModelStateDictionary modelState)
    {
        return LocalizedProblemDetails.FromModelState(HttpContext, modelState, messages);
    }

    private ValidationProblemDetails CreateValidationProblem(
        IDictionary<string, string[]> errors)
    {
        var errorCodes = errors.ToDictionary(x => x.Key, x => x.Value.Select(_ => ApiErrorCodes.InvalidInput).ToArray());
        return LocalizedProblemDetails.FromErrors(HttpContext, errors.ToDictionary(x => x.Key, x => x.Value), errorCodes, messages);
    }

    private async Task<IActionResult> Conflict(BookingConflict conflict)
    {
        var room = await rooms.FindAsync(conflict.RoomId);
        var response = new ConflictResponse(
            conflict.RoomId.Value,
            conflict.RequestedStartUtc,
            conflict.RequestedEndUtc,
            conflict.ConflictingStartUtc,
            conflict.ConflictingEndUtc,
            conflict.Alternatives.Select(x => new AlternativeResponse(x.StartsAtUtc, x.EndsAtUtc)).ToArray(),
            room?.TimeZone.Id ?? "UTC",
            conflict.Alternatives.Count == 0 ? ApiErrorCodes.NoAlternatives : ApiErrorCodes.Conflict,
            messages[conflict.Alternatives.Count == 0 ? ApiErrorCodes.NoAlternatives : ApiErrorCodes.Conflict].Value);
        return StatusCode(StatusCodes.Status409Conflict, response);
    }

    private async Task<BookingResponse> ToResponse(Booking booking)
    {
        var room = await rooms.FindAsync(booking.RoomId);
        var zone = room?.TimeZone ?? TimeZoneInfo.Utc;
        return new BookingResponse(booking.Id.Value, booking.RoomId.Value, booking.MemberId,
            booking.StartsAtUtc, booking.EndsAtUtc,
            TimeZoneInfo.ConvertTime(booking.StartsAtUtc, zone), TimeZoneInfo.ConvertTime(booking.EndsAtUtc, zone), zone.Id);
    }
}

public sealed class CreateBookingRequest
{
    [JsonConstructor]
    public CreateBookingRequest(Guid roomId, string? startsAt, string? endsAt)
    {
        RoomId = roomId;
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    public CreateBookingRequest(Guid roomId, DateTimeOffset startsAt, DateTimeOffset endsAt)
        : this(roomId, startsAt.ToString("O", CultureInfo.InvariantCulture),
            endsAt.ToString("O", CultureInfo.InvariantCulture)) { }

    public Guid RoomId { get; }
    public string? StartsAt { get; }
    public string? EndsAt { get; }
}
public sealed record BookingResponse(Guid Id, Guid RoomId, Guid MemberId, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc,
    DateTimeOffset StartsAtLocal, DateTimeOffset EndsAtLocal, string TimeZone);
public sealed record AlternativeResponse(DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc);
public sealed record ConflictResponse(Guid RoomId, DateTimeOffset RequestedStartUtc, DateTimeOffset RequestedEndUtc,
    DateTimeOffset ConflictingStartUtc, DateTimeOffset ConflictingEndUtc, IReadOnlyList<AlternativeResponse> Alternatives,
    string TimeZone, string Code, string Message);
