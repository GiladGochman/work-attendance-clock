using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkClock.Api.Data;
using WorkClock.Api.Dtos;
using WorkClock.Api.Exceptions;
using WorkClock.Api.Models;
using WorkClock.Api.Services;

namespace WorkClock.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AttendanceController(AppDbContext db, ITimeService timeService, ILogger<AttendanceController> logger)
    : ControllerBase
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private string? ClientIp =>
        HttpContext.Connection.RemoteIpAddress?.ToString();

    private static AttendanceRecordDto ToDto(AttendanceRecord r) => new()
    {
        Id              = r.Id,
        EmployeeId      = r.EmployeeId,
        ClockInUtc      = r.ClockIn,
        ClockOutUtc     = r.ClockOut,
        DurationMinutes = r.ClockIn.HasValue && r.ClockOut.HasValue
            ? (r.ClockOut.Value - r.ClockIn.Value).TotalMinutes
            : null,
        SourceIp        = r.SourceIp,
    };

    /// <summary>Find an open (no clock-out) record for <paramref name="employeeId"/> that started today (UTC).</summary>
    private Task<AttendanceRecord?> FindActiveRecordAsync(string employeeId, DateTime todayUtc) =>
        db.AttendanceRecords
          .Where(r => r.EmployeeId == employeeId
                   && r.ClockIn >= todayUtc
                   && r.ClockIn < todayUtc.AddDays(1)
                   && r.ClockOut == null)
          .FirstOrDefaultAsync();

    // ── POST /api/attendance/clockin ──────────────────────────────────────────

    /// <summary>
    /// Registers a clock-in for the given employee.
    /// Returns 409 if the employee already has an open session today.
    /// Returns 503 if the external time API is unavailable.
    /// </summary>
    [HttpPost("clockin")]
    [ProducesResponseType(typeof(AttendanceRecordDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ClockIn([FromBody] ClockRequestDto request)
    {
        DateTime utcNow;
        try
        {
            utcNow = await timeService.GetUtcNowAsync();
        }
        catch (TimeServiceException ex)
        {
            logger.LogWarning(ex, "ClockIn blocked – could not fetch authoritative time.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = ex.Message });
        }

        var existing = await FindActiveRecordAsync(request.EmployeeId, utcNow.Date);
        if (existing is not null)
        {
            return Conflict(new
            {
                error   = "Already clocked in for today.",
                clockIn = existing.ClockIn
            });
        }

        var record = new AttendanceRecord
        {
            EmployeeId = request.EmployeeId,
            ClockIn    = utcNow,
            SourceIp   = ClientIp,
        };

        db.AttendanceRecords.Add(record);
        await db.SaveChangesAsync();

        logger.LogInformation("ClockIn: employee={EmployeeId} at {UtcNow} from {Ip}",
            record.EmployeeId, utcNow, ClientIp);

        return CreatedAtAction(nameof(GetHistory),
            new { employeeId = record.EmployeeId },
            ToDto(record));
    }

    // ── POST /api/attendance/clockout ─────────────────────────────────────────

    /// <summary>
    /// Registers a clock-out for the given employee's active session.
    /// Returns 400 if there is no open clock-in for today.
    /// Returns 503 if the external time API is unavailable.
    /// </summary>
    [HttpPost("clockout")]
    [ProducesResponseType(typeof(AttendanceRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ClockOut([FromBody] ClockRequestDto request)
    {
        DateTime utcNow;
        try
        {
            utcNow = await timeService.GetUtcNowAsync();
        }
        catch (TimeServiceException ex)
        {
            logger.LogWarning(ex, "ClockOut blocked – could not fetch authoritative time.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = ex.Message });
        }

        var record = await FindActiveRecordAsync(request.EmployeeId, utcNow.Date);
        if (record is null)
        {
            return BadRequest(new
            {
                error = "No active clock-in found for today. Please clock in first."
            });
        }

        record.ClockOut = utcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("ClockOut: employee={EmployeeId} at {UtcNow} (duration={Duration:mm\\:ss})",
            record.EmployeeId, utcNow, record.ClockOut - record.ClockIn);

        return Ok(ToDto(record));
    }

    // ── GET /api/attendance/history ───────────────────────────────────────────

    /// <summary>Returns all attendance records for the given employee, newest first.</summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(IEnumerable<AttendanceRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory([FromQuery] string employeeId)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            return BadRequest(new { error = "employeeId is required." });

        var records = await db.AttendanceRecords
            .Where(r => r.EmployeeId == employeeId)
            .OrderByDescending(r => r.ClockIn)
            .Select(r => ToDto(r))
            .ToListAsync();

        return Ok(records);
    }
}
