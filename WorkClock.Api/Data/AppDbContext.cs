using Microsoft.EntityFrameworkCore;
using WorkClock.Api.Models;

namespace WorkClock.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EmployeeId)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(e => e.SourceIp)
                  .HasMaxLength(45); // supports IPv6

            // Explicitly tell EF Core to treat these DateTime columns as UTC
            entity.Property(e => e.ClockIn)
                  .HasColumnType("datetime2");

            entity.Property(e => e.ClockOut)
                  .HasColumnType("datetime2");

            // Index used by the "active record" lookup on every ClockOut call
            entity.HasIndex(e => new { e.EmployeeId, e.ClockIn });
        });
    }
}
