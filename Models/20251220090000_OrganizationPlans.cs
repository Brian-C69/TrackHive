using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackHive.Models
{
    /// <inheritdoc />
    public partial class OrganizationPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentPlan",
                table: "Organizations",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Free");

            migrationBuilder.AddColumn<DateTime>(
                name: "BillingPeriodStartUtc",
                table: "Organizations",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentPeriodEndsUtc",
                table: "Organizations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialEndsUtc",
                table: "Organizations",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingPeriodStartUtc",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "CurrentPeriodEndsUtc",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "CurrentPlan",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "TrialEndsUtc",
                table: "Organizations");
        }
    }
}
