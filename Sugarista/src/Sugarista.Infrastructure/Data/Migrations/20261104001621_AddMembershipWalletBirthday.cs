using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMembershipWalletBirthday : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "WalletTransactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BirthdayDiscountEnabled",
                table: "SiteSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BirthdayDiscountMode",
                table: "SiteSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "BirthdayDiscountPercent",
                table: "SiteSettings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "BirthdayDiscountWindowDays",
                table: "SiteSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "BirthdayEditableByCustomer",
                table: "SiteSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "BirthdayGreetingMessage",
                table: "SiteSettings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "MembershipDowngradeEnabled",
                table: "SiteSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "WalletCashbackExpiryDays",
                table: "SiteSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "WalletCashbackMaxPerOrderAmount",
                table: "SiteSettings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "WalletPaymentMaxPercent",
                table: "SiteSettings",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<decimal>(
                name: "CashbackPercent",
                table: "MembershipTiers",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinPaidOrders",
                table: "MembershipTiers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "Customers",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WalletTopUpPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BonusAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTopUpPackages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_ExpiresAt",
                table: "WalletTransactions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTopUpPackages_DisplayOrder",
                table: "WalletTopUpPackages",
                column: "DisplayOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WalletTopUpPackages");

            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_ExpiresAt",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "BirthdayDiscountEnabled",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "BirthdayDiscountMode",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "BirthdayDiscountPercent",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "BirthdayDiscountWindowDays",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "BirthdayEditableByCustomer",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "BirthdayGreetingMessage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "MembershipDowngradeEnabled",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "WalletCashbackExpiryDays",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "WalletCashbackMaxPerOrderAmount",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "WalletPaymentMaxPercent",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "CashbackPercent",
                table: "MembershipTiers");

            migrationBuilder.DropColumn(
                name: "MinPaidOrders",
                table: "MembershipTiers");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Customers");
        }
    }
}
