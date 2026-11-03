using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAbandonedCarts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Defaults mirror the entity defaults so existing cafés get a
            // working feature right after the migration (the admin can change
            // or disable everything from the settings page).
            migrationBuilder.AddColumn<int>(
                name: "AbandonedCartCodeValidityHours",
                table: "SiteSettings",
                type: "integer",
                nullable: false,
                defaultValue: 48);

            migrationBuilder.AddColumn<decimal>(
                name: "AbandonedCartDiscountPercent",
                table: "SiteSettings",
                type: "numeric",
                nullable: false,
                defaultValue: 10m);

            migrationBuilder.AddColumn<bool>(
                name: "AbandonedCartEmailEnabled",
                table: "SiteSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AbandonedCartEmailSubject",
                table: "SiteSettings",
                type: "text",
                nullable: false,
                defaultValue: "سبد خرید شما در {cafe} منتظر است");

            migrationBuilder.AddColumn<string>(
                name: "AbandonedCartEmailTemplate",
                table: "SiteSettings",
                type: "text",
                nullable: false,
                defaultValue: "سلام {name}،\nسبد خرید شما در {cafe} ذخیره شده است:\n{items}\n\nکد تخفیف یک‌بارمصرف شما: {code}\nمیزان تخفیف: {percent}٪\nاعتبار کد: {validityHours} ساعت\n\nمنتظر حضور شما هستیم.");

            migrationBuilder.AddColumn<int>(
                name: "AbandonedCartHours",
                table: "SiteSettings",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<bool>(
                name: "AbandonedCartReminderEnabled",
                table: "SiteSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AbandonedCartSmsEnabled",
                table: "SiteSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "AbandonedCartSmsTemplate",
                table: "SiteSettings",
                type: "text",
                nullable: false,
                defaultValue: "سلام {name}، سبد خرید شما در {cafe} منتظر شماست. با کد «{code}» تا {validityHours} ساعت آینده {percent}٪ تخفیف بگیرید.");

            migrationBuilder.CreateTable(
                name: "AbandonedCarts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CartToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    ItemsTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ItemCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReminderSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReminderCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReminderChannel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ConvertedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbandonedCarts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AbandonedCarts_CartToken_Status",
                table: "AbandonedCarts",
                columns: new[] { "CartToken", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AbandonedCarts_Status_LastActivityAt",
                table: "AbandonedCarts",
                columns: new[] { "Status", "LastActivityAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbandonedCarts");

            migrationBuilder.DropColumn(
                name: "AbandonedCartCodeValidityHours",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AbandonedCartDiscountPercent",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AbandonedCartEmailEnabled",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AbandonedCartEmailSubject",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AbandonedCartEmailTemplate",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AbandonedCartHours",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AbandonedCartReminderEnabled",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AbandonedCartSmsEnabled",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AbandonedCartSmsTemplate",
                table: "SiteSettings");
        }
    }
}
