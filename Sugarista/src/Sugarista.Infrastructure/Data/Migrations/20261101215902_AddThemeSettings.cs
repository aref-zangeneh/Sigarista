using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddThemeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowManualThemeOverride",
                table: "SiteSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "DarkThemeStartHour",
                table: "SiteSettings",
                type: "integer",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.AddColumn<int>(
                name: "LightThemeStartHour",
                table: "SiteSettings",
                type: "integer",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<string>(
                name: "ThemeMode",
                table: "SiteSettings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "auto");

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "SiteSettings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Asia/Tehran");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowManualThemeOverride",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "DarkThemeStartHour",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "LightThemeStartHour",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "ThemeMode",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "SiteSettings");
        }
    }
}
