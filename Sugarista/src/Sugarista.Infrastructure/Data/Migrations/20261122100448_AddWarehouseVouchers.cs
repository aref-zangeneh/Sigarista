using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WarehouseVouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    VoucherType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Posted"),
                    Reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    CounterpartyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExternalReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VoucherDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseVouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseVouchers_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WarehouseVouchers_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseVoucherLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    StockBefore = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    StockAfter = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LineNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseVoucherLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseVoucherLines_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseVoucherLines_WarehouseVouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "WarehouseVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseVoucherLines_ExpirationDate",
                table: "WarehouseVoucherLines",
                column: "ExpirationDate");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseVoucherLines_IngredientId",
                table: "WarehouseVoucherLines",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseVoucherLines_VoucherId_IngredientId",
                table: "WarehouseVoucherLines",
                columns: new[] { "VoucherId", "IngredientId" });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseVouchers_Status",
                table: "WarehouseVouchers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseVouchers_SupplierId",
                table: "WarehouseVouchers",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseVouchers_VoucherDate",
                table: "WarehouseVouchers",
                column: "VoucherDate");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseVouchers_VoucherNumber",
                table: "WarehouseVouchers",
                column: "VoucherNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseVouchers_VoucherType",
                table: "WarehouseVouchers",
                column: "VoucherType");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseVouchers_WarehouseId",
                table: "WarehouseVouchers",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WarehouseVoucherLines");

            migrationBuilder.DropTable(
                name: "WarehouseVouchers");
        }
    }
}
