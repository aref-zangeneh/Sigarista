using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderManualDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ManualDiscountAmount",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ManualDiscountAppliedBy",
                table: "Orders",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualDiscountReason",
                table: "Orders",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualDiscountType",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ManualDiscountValue",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualDiscountAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ManualDiscountAppliedBy",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ManualDiscountReason",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ManualDiscountType",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ManualDiscountValue",
                table: "Orders");
        }
    }
}
