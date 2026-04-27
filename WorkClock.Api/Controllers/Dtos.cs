using WorkClock.Api.Models;

namespace WorkClock.Api.Controllers;

public record ClockRequest(string EmployeeId);

public record ClockResponse(
    int Id,
    string EmployeeId,
    DateTime? ClockInUtc,
    DateTime? ClockOutUtc,
    string Status)
{
    public static ClockResponse From(AttendanceRecord r) => new(
        r.Id,
        r.EmployeeId,
        r.ClockIn,
        r.ClockOut,
        r.ClockOut.HasValue ? "ClockedOut" : "ClockedIn");
}
