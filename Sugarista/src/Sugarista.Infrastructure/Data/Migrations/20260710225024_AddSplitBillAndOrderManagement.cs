using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSplitBillAndOrderManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderLineId",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AmountPaid",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GuestCount",
                table: "Orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSplitBill",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "InventoryDeductedQuantity",
                table: "OrderLines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PayerKey",
                table: "OrderLines",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayerLabel",
                table: "OrderLines",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayerKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PayerLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    PaymentAuthority = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderPayments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OrderId",
                table: "StockMovements",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OrderLineId",
                table: "StockMovements",
                column: "OrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_IsSplitBill",
                table: "Orders",
                column: "IsSplitBill");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPayments_OrderId_PayerKey",
                table: "OrderPayments",
                columns: new[] { "OrderId", "PayerKey" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderPayments_PaymentAuthority",
                table: "OrderPayments",
                column: "PaymentAuthority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderPayments");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_OrderId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_OrderLineId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_Orders_IsSplitBill",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "OrderLineId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "GuestCount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsSplitBill",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InventoryDeductedQuantity",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "PayerKey",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "PayerLabel",
                table: "OrderLines");
        }
    }
}
