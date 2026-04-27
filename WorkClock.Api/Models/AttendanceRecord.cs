namespace WorkClock.Api.Models;

/// <summary>
/// Represents a single clock-in/clock-out pair for one employee on one day.
/// ClockIn and ClockOut are stored as UTC in the database.
/// </summary>
public class AttendanceRecord
{
    public int Id { get; set; }

    /// <summary>Identifier supplied by the client (e.g. employee number or username).</summary>
    public string EmployeeId { get; set; } = string.Empty;

    /// <summary>UTC instant when the employee clocked in.</summary>
    public DateTime? ClockIn { get; set; }

    /// <summary>UTC instant when the employee clocked out. Null until the employee clocks out.</summary>
    public DateTime? ClockOut { get; set; }

    /// <summary>IP address of the client that initiated the request, for audit purposes.</summary>
    public string? SourceIp { get; set; }
}
