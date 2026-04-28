namespace WorkClock.Api.Dtos;

/// <summary>
/// API response shape. All times are UTC ISO-8601 strings.
/// The client is responsible for converting to local time for display.
/// </summary>
public class AttendanceRecordDto
{
    public int    Id               { get; init; }
    public string EmployeeId       { get; init; } = string.Empty;

    /// <summary>UTC clock-in instant (ISO-8601).</summary>
    public DateTime? ClockInUtc    { get; init; }

    /// <summary>UTC clock-out instant (ISO-8601). Null if still active.</summary>
    public DateTime? ClockOutUtc   { get; init; }

    /// <summary>Total session length in minutes. Null if not yet clocked out.</summary>
    public double?   DurationMinutes { get; init; }

    public string?   SourceIp      { get; init; }
}
