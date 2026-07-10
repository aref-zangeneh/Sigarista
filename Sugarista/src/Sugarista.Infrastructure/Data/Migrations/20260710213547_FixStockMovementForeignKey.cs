using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixStockMovementForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_InventoryStocks_IngredientId",
                table: "StockMovements");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_InventoryStocks_IngredientId",
                table: "InventoryStocks",
                column: "IngredientId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_InventoryStocks_IngredientId",
                table: "StockMovements",
                column: "IngredientId",
                principalTable: "InventoryStocks",
                principalColumn: "IngredientId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_InventoryStocks_IngredientId",
                table: "StockMovements");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_InventoryStocks_IngredientId",
                table: "InventoryStocks");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_InventoryStocks_IngredientId",
                table: "StockMovements",
                column: "IngredientId",
                principalTable: "InventoryStocks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
