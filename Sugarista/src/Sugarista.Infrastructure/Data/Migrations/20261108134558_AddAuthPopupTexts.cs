using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthPopupTexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthPopupDescription",
                table: "SiteSettings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AuthPopupLoginButtonLabel",
                table: "SiteSettings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AuthPopupSkipButtonLabel",
                table: "SiteSettings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AuthPopupTitle",
                table: "SiteSettings",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthPopupDescription",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AuthPopupLoginButtonLabel",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AuthPopupSkipButtonLabel",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AuthPopupTitle",
                table: "SiteSettings");
        }
    }
}
