using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderKindAndAccountingExclusion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ComplimentaryReason",
                table: "Orders",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExcludedFromAccountingAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExcludedFromAccountingReason",
                table: "Orders",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExcludedFromAccounting",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OrderKind",
                table: "Orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Standard");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_IsExcludedFromAccounting",
                table: "Orders",
                column: "IsExcludedFromAccounting");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderKind",
                table: "Orders",
                column: "OrderKind");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_IsExcludedFromAccounting",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderKind",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ComplimentaryReason",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ExcludedFromAccountingAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ExcludedFromAccountingReason",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsExcludedFromAccounting",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderKind",
                table: "Orders");
        }
    }
}
