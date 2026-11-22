using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemPurchasePriceAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PurchasePrice",
                table: "MenuItems",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MenuItemPriceHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousPurchasePrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    NewPurchasePrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PurchasePriceDelta = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PurchasePriceChangePercent = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    PreviousSalePrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    NewSalePrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    SalePriceDelta = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    SalePriceChangePercent = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemPriceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItemPriceHistories_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemPriceHistories_MenuItemId_RecordedAt",
                table: "MenuItemPriceHistories",
                columns: new[] { "MenuItemId", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuItemPriceHistories");

            migrationBuilder.DropColumn(
                name: "PurchasePrice",
                table: "MenuItems");
        }
    }
}
