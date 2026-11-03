using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStampCard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "StampCardEnabled",
                table: "SiteSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "StampCardOrdersPerCard",
                table: "SiteSettings",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<decimal>(
                name: "StampCardRewardPercent",
                table: "SiteSettings",
                type: "numeric",
                nullable: false,
                defaultValue: 10m);

            migrationBuilder.AddColumn<int>(
                name: "StampCardRewardValidityDays",
                table: "SiteSettings",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.CreateTable(
                name: "StampCardRewards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullCardNumber = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RewardPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    IsRedeemed = table.Column<bool>(type: "boolean", nullable: false),
                    RedeemedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RedeemedOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StampCardRewards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StampCardRewards_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StampCardRewards_Code",
                table: "StampCardRewards",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_StampCardRewards_CustomerId",
                table: "StampCardRewards",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_StampCardRewards_CustomerId_FullCardNumber",
                table: "StampCardRewards",
                columns: new[] { "CustomerId", "FullCardNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StampCardRewards");

            migrationBuilder.DropColumn(
                name: "StampCardEnabled",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "StampCardOrdersPerCard",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "StampCardRewardPercent",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "StampCardRewardValidityDays",
                table: "SiteSettings");
        }
    }
}
