using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryPurchases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryPurchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    PackageCount = table.Column<int>(type: "integer", nullable: true),
                    PackageSize = table.Column<decimal>(type: "numeric(12,3)", nullable: true),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    PurchaseUnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ShippingFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    IncidentalCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DocumentImageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StockMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryPurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryPurchases_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryPurchases_StockMovements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalTable: "StockMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPurchases_ExpirationDate",
                table: "InventoryPurchases",
                column: "ExpirationDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPurchases_IngredientId_ExpirationDate",
                table: "InventoryPurchases",
                columns: new[] { "IngredientId", "ExpirationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPurchases_ReceivedAt",
                table: "InventoryPurchases",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPurchases_StockMovementId",
                table: "InventoryPurchases",
                column: "StockMovementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryPurchases");
        }
    }
}
