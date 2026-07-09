using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeMenuCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "Allergens",
                table: "MenuItems",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<int>(
                name: "Calories",
                table: "MenuItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnglishName",
                table: "MenuItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string[]>(
                name: "GalleryImageUrls",
                table: "MenuItems",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "MenuItems",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOrderable",
                table: "MenuItems",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "LargePrice",
                table: "MenuItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LargeSpecialOfferPrice",
                table: "MenuItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LargeVolume",
                table: "MenuItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediumPrice",
                table: "MenuItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediumSpecialOfferPrice",
                table: "MenuItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediumVolume",
                table: "MenuItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersianName",
                table: "MenuItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PreparingDurationMinutes",
                table: "MenuItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmallPrice",
                table: "MenuItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmallSpecialOfferPrice",
                table: "MenuItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmallVolume",
                table: "MenuItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpecialOfferPrice",
                table: "MenuItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxPercent",
                table: "MenuItems",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Volume",
                table: "MenuItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "MenuItems"
                SET
                    "PersianName" = "Name",
                    "EnglishName" = "Name",
                    "MediumPrice" = "Price",
                    "IsOrderable" = TRUE
                WHERE "PersianName" = '' OR "EnglishName" = '' OR "MediumPrice" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Allergens",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "Calories",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "EnglishName",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "GalleryImageUrls",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "IsOrderable",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "LargePrice",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "LargeSpecialOfferPrice",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "LargeVolume",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "MediumPrice",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "MediumSpecialOfferPrice",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "MediumVolume",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "PersianName",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "PreparingDurationMinutes",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "SmallPrice",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "SmallSpecialOfferPrice",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "SmallVolume",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "SpecialOfferPrice",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "TaxPercent",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "Volume",
                table: "MenuItems");
        }
    }
}
