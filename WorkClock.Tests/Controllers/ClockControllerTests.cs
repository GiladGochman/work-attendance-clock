using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WorkClock.Api.Controllers;
using WorkClock.Api.Data;
using WorkClock.Api.Exceptions;
using WorkClock.Api.Models;
using WorkClock.Api.Services;
using Xunit;

namespace WorkClock.Tests.Controllers;

/// <summary>
/// Unit tests for ClockController.
/// Uses EF Core InMemory database (no SQL Server required) and a mocked ITimeService.
/// </summary>
public class ClockControllerTests : IDisposable
{
    private static readonly DateTime FakeUtcNow = new(2026, 4, 27, 10, 0, 0, DateTimeKind.Utc);

    private readonly AppDbContext _db;
    private readonly Mock<ITimeService> _timeService;
    private readonly ClockController _sut;

    public ClockControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // isolated DB per test
            .Options;

        _db = new AppDbContext(options);
        _timeService = new Mock<ITimeService>();

        _sut = new ClockController(_db, _timeService.Object, NullLogger<ClockController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    public void Dispose() => _db.Dispose();

    // ── Clock In ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClockIn_Success_Creates_Record_Returns201()
    {
        _timeService.Setup(s => s.GetUtcNowAsync()).ReturnsAsync(FakeUtcNow);

        var result = await _sut.ClockIn(new ClockRequest("EMP001"));

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var body = Assert.IsType<ClockResponse>(created.Value);
        Assert.Equal("EMP001", body.EmployeeId);
        Assert.Equal(FakeUtcNow, body.ClockInUtc);
        Assert.Null(body.ClockOutUtc);
        Assert.Equal("ClockedIn", body.Status);
        Assert.Equal(1, await _db.AttendanceRecords.CountAsync());
    }

    [Fact]
    public async Task ClockIn_AlreadyClockedIn_Returns409_And_DoesNotCallTimeService()
    {
        _db.AttendanceRecords.Add(new AttendanceRecord
        {
            EmployeeId = "EMP001",
            ClockIn    = FakeUtcNow.AddHours(-4)
        });
        await _db.SaveChangesAsync();

        var result = await _sut.ClockIn(new ClockRequest("EMP001"));

        Assert.IsType<ConflictObjectResult>(result);
        _timeService.Verify(s => s.GetUtcNowAsync(), Times.Never);
        Assert.Equal(1, await _db.AttendanceRecords.CountAsync()); // no extra record
    }

    [Fact]
    public async Task ClockIn_EmptyEmployeeId_Returns400()
    {
        var result = await _sut.ClockIn(new ClockRequest("   "));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ClockIn_TimeServiceDown_Returns503_And_NoRecordSaved()
    {
        _timeService.Setup(s => s.GetUtcNowAsync())
                    .ThrowsAsync(new TimeServiceException("External time API unavailable."));

        var result = await _sut.ClockIn(new ClockRequest("EMP001"));

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, status.StatusCode);
        Assert.Equal(0, await _db.AttendanceRecords.CountAsync());
    }

    [Fact]
    public async Task ClockIn_CanClockIn_After_PreviousShiftEnded()
    {
        // A completed (clocked-out) record should not block a new clock-in
        _db.AttendanceRecords.Add(new AttendanceRecord
        {
            EmployeeId = "EMP001",
            ClockIn    = FakeUtcNow.AddDays(-1),
            ClockOut   = FakeUtcNow.AddDays(-1).AddHours(8)
        });
        await _db.SaveChangesAsync();
        _timeService.Setup(s => s.GetUtcNowAsync()).ReturnsAsync(FakeUtcNow);

        var result = await _sut.ClockIn(new ClockRequest("EMP001"));

        Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(2, await _db.AttendanceRecords.CountAsync());
    }

    // ── Clock Out ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClockOut_Success_Sets_ClockOut_Returns200()
    {
        _db.AttendanceRecords.Add(new AttendanceRecord
        {
            EmployeeId = "EMP001",
            ClockIn    = FakeUtcNow.AddHours(-8)
        });
        await _db.SaveChangesAsync();
        _timeService.Setup(s => s.GetUtcNowAsync()).ReturnsAsync(FakeUtcNow);

        var result = await _sut.ClockOut(new ClockRequest("EMP001"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ClockResponse>(ok.Value);
        Assert.Equal(FakeUtcNow, body.ClockOutUtc);
        Assert.Equal("ClockedOut", body.Status);
    }

    [Fact]
    public async Task ClockOut_NotClockedIn_Returns409()
    {
        var result = await _sut.ClockOut(new ClockRequest("EMP001"));
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task ClockOut_AlreadyClockedOut_Returns409()
    {
        // Only open records (ClockOut == null) qualify for clock-out
        _db.AttendanceRecords.Add(new AttendanceRecord
        {
            EmployeeId = "EMP001",
            ClockIn    = FakeUtcNow.AddHours(-8),
            ClockOut   = FakeUtcNow.AddHours(-1)
        });
        await _db.SaveChangesAsync();

        var result = await _sut.ClockOut(new ClockRequest("EMP001"));

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task ClockOut_TimeServiceDown_Returns503_And_DoesNotModifyRecord()
    {
        var record = new AttendanceRecord { EmployeeId = "EMP001", ClockIn = FakeUtcNow.AddHours(-8) };
        _db.AttendanceRecords.Add(record);
        await _db.SaveChangesAsync();
        _timeService.Setup(s => s.GetUtcNowAsync())
                    .ThrowsAsync(new TimeServiceException("External time API unavailable."));

        var result = await _sut.ClockOut(new ClockRequest("EMP001"));

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, status.StatusCode);

        // Record must not have been modified
        _db.ChangeTracker.Clear();
        var unchanged = await _db.AttendanceRecords.FindAsync(record.Id);
        Assert.Null(unchanged!.ClockOut);
    }

    [Fact]
    public async Task ClockOut_EmptyEmployeeId_Returns400()
    {
        var result = await _sut.ClockOut(new ClockRequest(""));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── Get Status ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatus_ClockedIn_ReturnsCorrectStatus()
    {
        _db.AttendanceRecords.Add(new AttendanceRecord { EmployeeId = "EMP001", ClockIn = FakeUtcNow });
        await _db.SaveChangesAsync();

        var result = await _sut.GetStatus("EMP001");

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ClockResponse>(ok.Value);
        Assert.Equal("ClockedIn", body.Status);
    }

    [Fact]
    public async Task GetStatus_ClockedOut_ReturnsCorrectStatus()
    {
        _db.AttendanceRecords.Add(new AttendanceRecord
        {
            EmployeeId = "EMP001",
            ClockIn    = FakeUtcNow.AddHours(-8),
            ClockOut   = FakeUtcNow
        });
        await _db.SaveChangesAsync();

        var result = await _sut.GetStatus("EMP001");

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ClockResponse>(ok.Value);
        Assert.Equal("ClockedOut", body.Status);
    }

    [Fact]
    public async Task GetStatus_NoRecord_Returns404()
    {
        var result = await _sut.GetStatus("UNKNOWN_EMP");
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetStatus_MultipleRecords_ReturnsMostRecent()
    {
        // Two complete shifts — should return the second (newer) one
        _db.AttendanceRecords.AddRange(
            new AttendanceRecord { EmployeeId = "EMP001", ClockIn = FakeUtcNow.AddDays(-2), ClockOut = FakeUtcNow.AddDays(-2).AddHours(8) },
            new AttendanceRecord { EmployeeId = "EMP001", ClockIn = FakeUtcNow.AddDays(-1), ClockOut = FakeUtcNow.AddDays(-1).AddHours(8) }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetStatus("EMP001");

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ClockResponse>(ok.Value);
        Assert.Equal(FakeUtcNow.AddDays(-1), body.ClockInUtc);
    }

    // ── Get History ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHistory_ReturnsOnlyThisEmployee_OrderedNewestFirst()
    {
        _db.AttendanceRecords.AddRange(
            new AttendanceRecord { EmployeeId = "EMP001", ClockIn = FakeUtcNow.AddDays(-2), ClockOut = FakeUtcNow.AddDays(-2).AddHours(8) },
            new AttendanceRecord { EmployeeId = "EMP001", ClockIn = FakeUtcNow.AddDays(-1), ClockOut = FakeUtcNow.AddDays(-1).AddHours(8) },
            new AttendanceRecord { EmployeeId = "EMP002", ClockIn = FakeUtcNow } // different employee
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetHistory("EMP001");

        var ok = Assert.IsType<OkObjectResult>(result);
        var records = Assert.IsAssignableFrom<IEnumerable<ClockResponse>>(ok.Value).ToList();
        Assert.Equal(2, records.Count);
        Assert.True(records[0].ClockInUtc > records[1].ClockInUtc, "Records should be newest first.");
        Assert.All(records, r => Assert.Equal("EMP001", r.EmployeeId));
    }

    [Fact]
    public async Task GetHistory_NoRecords_ReturnsEmptyList()
    {
        var result = await _sut.GetHistory("EMP_NEW");

        var ok = Assert.IsType<OkObjectResult>(result);
        var records = Assert.IsAssignableFrom<IEnumerable<ClockResponse>>(ok.Value).ToList();
        Assert.Empty(records);
    }
}
