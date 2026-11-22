using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WarehouseTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SourceWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    DestinationWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TransferredBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    TransferredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseTransfers_Warehouses_DestinationWarehouseId",
                        column: x => x.DestinationWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseTransfers_Warehouses_SourceWarehouseId",
                        column: x => x.SourceWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseTransferLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    SourceQuantityBefore = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    SourceQuantityAfter = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    DestinationQuantityBefore = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    DestinationQuantityAfter = table.Column<decimal>(type: "numeric(12,3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseTransferLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseTransferLines_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseTransferLines_WarehouseTransfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "WarehouseTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransferLines_IngredientId",
                table: "WarehouseTransferLines",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransferLines_TransferId_IngredientId",
                table: "WarehouseTransferLines",
                columns: new[] { "TransferId", "IngredientId" });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_DestinationWarehouseId",
                table: "WarehouseTransfers",
                column: "DestinationWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_ReferenceNumber",
                table: "WarehouseTransfers",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_SourceWarehouseId",
                table: "WarehouseTransfers",
                column: "SourceWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_TransferredAt",
                table: "WarehouseTransfers",
                column: "TransferredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WarehouseTransferLines");

            migrationBuilder.DropTable(
                name: "WarehouseTransfers");
        }
    }
}
