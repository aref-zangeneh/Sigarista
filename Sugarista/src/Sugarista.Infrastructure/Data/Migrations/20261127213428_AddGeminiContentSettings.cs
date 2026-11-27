using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGeminiContentSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GeminiApiKey",
                table: "SiteSettings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GeminiModel",
                table: "SiteSettings",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "gemini-2.5-flash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GeminiApiKey",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "GeminiModel",
                table: "SiteSettings");
        }
    }
}
