using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackHive.Models
{
    /// <inheritdoc />
    public partial class Sync_20250916 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_UserId",
                table: "AttendanceRecords");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_UserId",
                table: "AttendanceRecords",
                column: "UserId");
        }
    }
}
