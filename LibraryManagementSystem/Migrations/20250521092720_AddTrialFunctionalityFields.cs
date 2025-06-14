using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraryManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddTrialFunctionalityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTrialPeriod",
                table: "UserSubscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RenewalReminderSent",
                table: "UserSubscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialEndDate",
                table: "UserSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TrialReminderSent",
                table: "UserSubscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FreeTrialDays",
                table: "SubscriptionPlans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "HasFreeTrial",
                table: "SubscriptionPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTrialPeriod",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "RenewalReminderSent",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "TrialEndDate",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "TrialReminderSent",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "FreeTrialDays",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "HasFreeTrial",
                table: "SubscriptionPlans");
        }
    }
}
