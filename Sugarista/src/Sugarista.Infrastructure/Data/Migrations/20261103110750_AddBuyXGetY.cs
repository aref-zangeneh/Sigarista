using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBuyXGetY : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BuyQuantity",
                table: "Discounts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "FreeItemSelection",
                table: "Discounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GetQuantity",
                table: "Discounts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "MaxApplications",
                table: "Discounts",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuyQuantity",
                table: "Discounts");

            migrationBuilder.DropColumn(
                name: "FreeItemSelection",
                table: "Discounts");

            migrationBuilder.DropColumn(
                name: "GetQuantity",
                table: "Discounts");

            migrationBuilder.DropColumn(
                name: "MaxApplications",
                table: "Discounts");
        }
    }
}
