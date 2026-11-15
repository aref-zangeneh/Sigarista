using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsPhase3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryWebhookSecret",
                table: "SmsProviders",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSmart",
                table: "SmsContactGroups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SmartFilterJson",
                table: "SmsContactGroups",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScheduledAt",
                table: "SmsCampaigns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderLine",
                table: "SmsCampaigns",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsCampaigns_Status_ScheduledAt",
                table: "SmsCampaigns",
                columns: new[] { "Status", "ScheduledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SmsCampaigns_Status_ScheduledAt",
                table: "SmsCampaigns");

            migrationBuilder.DropColumn(
                name: "DeliveryWebhookSecret",
                table: "SmsProviders");

            migrationBuilder.DropColumn(
                name: "IsSmart",
                table: "SmsContactGroups");

            migrationBuilder.DropColumn(
                name: "SmartFilterJson",
                table: "SmsContactGroups");

            migrationBuilder.DropColumn(
                name: "ScheduledAt",
                table: "SmsCampaigns");

            migrationBuilder.DropColumn(
                name: "SenderLine",
                table: "SmsCampaigns");
        }
    }
}
