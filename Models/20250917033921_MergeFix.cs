using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackHive.Models
{
    /// <inheritdoc />
    public partial class MergeFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollRecords_UserId",
                table: "PayrollRecords");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PayrollRecords_UserId",
                table: "PayrollRecords",
                column: "UserId");
        }
    }
}
