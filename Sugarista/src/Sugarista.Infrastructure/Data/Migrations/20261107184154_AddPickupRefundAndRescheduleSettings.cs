using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPickupRefundAndRescheduleSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PickupMaxReschedules",
                table: "SiteSettings",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<string>(
                name: "PickupRefundMode",
                table: "SiteSettings",
                type: "text",
                nullable: false,
                defaultValue: "Wallet");

            migrationBuilder.AddColumn<int>(
                name: "RescheduleCount",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Preserve the previous "auto refund on cancel" decision: cafés that
            // had it enabled keep refunding to the wallet, everyone else moves
            // to the new manual card-to-card flow.
            migrationBuilder.Sql(
                """
                UPDATE "SiteSettings"
                SET "PickupRefundMode" = CASE
                    WHEN "PickupAutoRefundOnCancel" THEN 'Wallet'
                    ELSE 'CardToCard'
                END;
                """);

            migrationBuilder.DropColumn(
                name: "PickupAutoRefundOnCancel",
                table: "SiteSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PickupAutoRefundOnCancel",
                table: "SiteSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(
                """
                UPDATE "SiteSettings"
                SET "PickupAutoRefundOnCancel" = ("PickupRefundMode" <> 'CardToCard');
                """);

            migrationBuilder.DropColumn(
                name: "PickupMaxReschedules",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PickupRefundMode",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "RescheduleCount",
                table: "Orders");
        }
    }
}
