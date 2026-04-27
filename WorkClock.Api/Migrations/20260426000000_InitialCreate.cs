using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkClock.Api.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AttendanceRecords",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                EmployeeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ClockIn    = table.Column<DateTime>(type: "datetime2", nullable: true),
                ClockOut   = table.Column<DateTime>(type: "datetime2", nullable: true),
                SourceIp   = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceRecords_EmployeeId_ClockIn",
            table: "AttendanceRecords",
            columns: new[] { "EmployeeId", "ClockIn" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AttendanceRecords");
    }
}
