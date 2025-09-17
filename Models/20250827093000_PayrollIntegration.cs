using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackHive.Models
{
    /// <inheritdoc />
    public partial class PayrollIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MonthlySalary",
                table: "Users",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "PayrollRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    MonthlySalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StandardHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WorkedHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AutoOvertimeHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdditionalOvertimeHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalOvertimeHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OvertimeMultiplier = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AttendancePay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OvertimePay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Deductions = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrossPay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetPay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WorkingDays = table.Column<int>(type: "int", nullable: false),
                    PresentDays = table.Column<int>(type: "int", nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRecords_UserId",
                table: "PayrollRecords",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRecords_UserId_Year_Month",
                table: "PayrollRecords",
                columns: new[] { "UserId", "Year", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollRecords");

            migrationBuilder.DropColumn(
                name: "MonthlySalary",
                table: "Users");
        }
    }
}
