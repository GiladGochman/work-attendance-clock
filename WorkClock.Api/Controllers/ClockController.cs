using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkClock.Api.Data;
using WorkClock.Api.Exceptions;
using WorkClock.Api.Models;
using WorkClock.Api.Services;

namespace WorkClock.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClockController(
    AppDbContext db,
    ITimeService timeService,
    ILogger<ClockController> logger) : ControllerBase
{
    /// <summary>Clock an employee in. Returns 409 if already clocked in.</summary>
    [HttpPost("in")]
    [ProducesResponseType(typeof(ClockResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ClockIn([FromBody] ClockRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeId))
            return BadRequest(new { message = "EmployeeId is required." });

        var openRecord = await db.AttendanceRecords
            .FirstOrDefaultAsync(r => r.EmployeeId == request.EmployeeId && r.ClockOut == null);

        if (openRecord != null)
            return Conflict(new { message = $"Employee '{request.EmployeeId}' is already clocked in." });

        DateTime utcNow;
        try
        {
            utcNow = await timeService.GetNowAsync();
        }
        catch (TimeServiceException ex)
        {
            logger.LogWarning(ex, "Time service unavailable during clock-in for {EmployeeId}.", request.EmployeeId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }

        var record = new AttendanceRecord
        {
            EmployeeId = request.EmployeeId,
            ClockIn    = utcNow,
            SourceIp   = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        db.AttendanceRecords.Add(record);
        await db.SaveChangesAsync();

        logger.LogInformation("Employee {EmployeeId} clocked in at {ClockIn} UTC.", record.EmployeeId, record.ClockIn);
        return CreatedAtAction(nameof(GetStatus), new { employeeId = record.EmployeeId }, ClockResponse.From(record));
    }

    /// <summary>Clock an employee out. Returns 409 if not currently clocked in.</summary>
    [HttpPost("out")]
    [ProducesResponseType(typeof(ClockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ClockOut([FromBody] ClockRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeId))
            return BadRequest(new { message = "EmployeeId is required." });

        var openRecord = await db.AttendanceRecords
            .FirstOrDefaultAsync(r => r.EmployeeId == request.EmployeeId && r.ClockOut == null);

        if (openRecord == null)
            return Conflict(new { message = $"Employee '{request.EmployeeId}' is not currently clocked in." });

        DateTime utcNow;
        try
        {
            utcNow = await timeService.GetNowAsync();
        }
        catch (TimeServiceException ex)
        {
            logger.LogWarning(ex, "Time service unavailable during clock-out for {EmployeeId}.", request.EmployeeId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }

        openRecord.ClockOut = utcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Employee {EmployeeId} clocked out at {ClockOut} UTC.", openRecord.EmployeeId, openRecord.ClockOut);
        return Ok(ClockResponse.From(openRecord));
    }

    /// <summary>Get the current clock status for an employee (most recent record).</summary>
    [HttpGet("status/{employeeId}")]
    [ProducesResponseType(typeof(ClockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(string employeeId)
    {
        var record = await db.AttendanceRecords
            .Where(r => r.EmployeeId == employeeId)
            .OrderByDescending(r => r.ClockIn)
            .FirstOrDefaultAsync();

        if (record == null)
            return NotFound(new { message = $"No attendance records found for employee '{employeeId}'." });

        return Ok(ClockResponse.From(record));
    }

    /// <summary>Get the full attendance history for an employee, newest first.</summary>
    [HttpGet("history/{employeeId}")]
    [ProducesResponseType(typeof(IEnumerable<ClockResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(string employeeId)
    {
        var records = await db.AttendanceRecords
            .Where(r => r.EmployeeId == employeeId)
            .OrderByDescending(r => r.ClockIn)
            .ToListAsync();

        return Ok(records.Select(ClockResponse.From));
    }
}
