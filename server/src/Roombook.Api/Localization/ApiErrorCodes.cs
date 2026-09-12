namespace Roombook.Api.Localization;

public static class ApiErrorCodes
{
    public const string Validation = "validation.failed";
    public const string InvalidInput = "validation.invalid";
    public const string RoomNotFound = "booking.room_not_found";
    public const string RoomUnavailable = "booking.room_unavailable";
    public const string StartInPast = "booking.start_in_past";
    public const string EndBeforeStart = "booking.end_before_start";
    public const string StartNotAligned = "booking.start_not_aligned";
    public const string EndNotAligned = "booking.end_not_aligned";
    public const string DurationTooShort = "booking.duration_too_short";
    public const string DurationTooLong = "booking.duration_too_long";
    public const string OutsideWorkingHours = "booking.outside_working_hours";
    public const string TimestampOffsetRequired = "booking.timestamp_offset_required";
    public const string Conflict = "booking.conflict";
    public const string NoAlternatives = "booking.no_alternatives";
    public const string Unauthorized = "authorization.required";
    public const string Forbidden = "authorization.forbidden";
    public const string NotFound = "resource.not_found";
    public const string Unexpected = "request.unexpected";

    public static string ForStatus(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => Unauthorized,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => NotFound,
        _ => Unexpected
    };
}
