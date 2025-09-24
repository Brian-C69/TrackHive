using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackHive.Models
{
    /// <inheritdoc />
    public partial class BillingSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubscriptionPlan",
                table: "Organizations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                table: "Organizations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                table: "Organizations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionRenewsAt",
                table: "Organizations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionUpdatedAt",
                table: "Organizations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_StripeSubscriptionId",
                table: "Organizations",
                column: "StripeSubscriptionId",
                unique: true,
                filter: "[StripeSubscriptionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Organizations_StripeSubscriptionId",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "SubscriptionPlan",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "StripeSubscriptionId",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "SubscriptionRenewsAt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "SubscriptionUpdatedAt",
                table: "Organizations");
        }
    }
}
