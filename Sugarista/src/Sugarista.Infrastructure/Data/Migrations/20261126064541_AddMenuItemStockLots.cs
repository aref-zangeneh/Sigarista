using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemStockLots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MenuItemStockLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    InitialQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BatchLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsDiscarded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DiscardedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DiscardReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemStockLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItemStockLots_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemStockLots_ExpiresAt",
                table: "MenuItemStockLots",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemStockLots_MenuItemId",
                table: "MenuItemStockLots",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemStockLots_MenuItemId_IsDiscarded_RemainingQuantity_~",
                table: "MenuItemStockLots",
                columns: new[] { "MenuItemId", "IsDiscarded", "RemainingQuantity", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuItemStockLots");
        }
    }
}
