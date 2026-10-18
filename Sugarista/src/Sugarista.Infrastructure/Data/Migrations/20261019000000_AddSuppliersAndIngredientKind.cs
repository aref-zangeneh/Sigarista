using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sugarista.Infrastructure.Data;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261019000000_AddSuppliersAndIngredientKind")]
    /// <inheritdoc />
    public partial class AddSuppliersAndIngredientKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Ingredients",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ingredient");

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                table: "InventoryPurchases",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ContactName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PersianName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IngredientSuppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPreferred = table.Column<bool>(type: "boolean", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientSuppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientSuppliers_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientSuppliers_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_Kind",
                table: "Ingredients",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPurchases_SupplierId",
                table: "InventoryPurchases",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_DisplayOrder",
                table: "Suppliers",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_PersianName",
                table: "Suppliers",
                column: "PersianName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSuppliers_IngredientId_SupplierId",
                table: "IngredientSuppliers",
                columns: new[] { "IngredientId", "SupplierId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSuppliers_SupplierId",
                table: "IngredientSuppliers",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryPurchases_Suppliers_SupplierId",
                table: "InventoryPurchases",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryPurchases_Suppliers_SupplierId",
                table: "InventoryPurchases");

            migrationBuilder.DropTable(
                name: "IngredientSuppliers");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_Kind",
                table: "Ingredients");

            migrationBuilder.DropIndex(
                name: "IX_InventoryPurchases_SupplierId",
                table: "InventoryPurchases");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "InventoryPurchases");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Ingredients");
        }
    }
}
