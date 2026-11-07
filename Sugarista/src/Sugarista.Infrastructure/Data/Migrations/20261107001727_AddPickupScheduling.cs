using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPickupScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PickupAutoRefundOnCancel",
                table: "SiteSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "PickupCancelCutoffMinutes",
                table: "SiteSettings",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<string>(
                name: "PickupCapacityMode",
                table: "SiteSettings",
                type: "text",
                nullable: false,
                defaultValue: "Orders");

            migrationBuilder.AddColumn<int>(
                name: "PickupMaxDaysAhead",
                table: "SiteSettings",
                type: "integer",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<int>(
                name: "PickupMinLeadMinutes",
                table: "SiteSettings",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "PickupRefundPolicyNote",
                table: "SiteSettings",
                type: "text",
                nullable: false,
                defaultValue: "در صورت لغو سفارش پرداخت‌شده تا مهلت مجاز، مبلغ طبق سیاست کافه به کیف پول شما برگشت داده می‌شود.");

            migrationBuilder.AddColumn<bool>(
                name: "PickupSchedulingEnabled",
                table: "SiteSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PickupSlotCapacity",
                table: "SiteSettings",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "PickupSlotMinutes",
                table: "SiteSettings",
                type: "integer",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DeliveryAt",
                table: "Orders",
                column: "DeliveryAt");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_IdempotencyKey",
                table: "Orders",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_DeliveryAt",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_IdempotencyKey",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PickupAutoRefundOnCancel",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PickupCancelCutoffMinutes",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PickupCapacityMode",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PickupMaxDaysAhead",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PickupMinLeadMinutes",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PickupRefundPolicyNote",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PickupSchedulingEnabled",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PickupSlotCapacity",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PickupSlotMinutes",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Orders");
        }
    }
}
