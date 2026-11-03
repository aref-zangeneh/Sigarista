using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMembershipTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MembershipTiersEnabled",
                table: "SiteSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "MembershipWindowDays",
                table: "SiteSettings",
                type: "integer",
                nullable: false,
                defaultValue: 365);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MembershipTierAssignedAt",
                table: "Customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MembershipTierId",
                table: "Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MembershipTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MinTotalPaidSpent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipTiers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_MembershipTierId",
                table: "Customers",
                column: "MembershipTierId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipTiers_DisplayOrder",
                table: "MembershipTiers",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipTiers_IsEnabled",
                table: "MembershipTiers",
                column: "IsEnabled");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_MembershipTiers_MembershipTierId",
                table: "Customers",
                column: "MembershipTierId",
                principalTable: "MembershipTiers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_MembershipTiers_MembershipTierId",
                table: "Customers");

            migrationBuilder.DropTable(
                name: "MembershipTiers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_MembershipTierId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "MembershipTiersEnabled",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "MembershipWindowDays",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "MembershipTierAssignedAt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "MembershipTierId",
                table: "Customers");
        }
    }
}
