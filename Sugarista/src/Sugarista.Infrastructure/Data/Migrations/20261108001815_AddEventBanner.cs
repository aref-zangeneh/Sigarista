using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventBanner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EventBannerEnabled",
                table: "SiteSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EventBannerEndsAt",
                table: "SiteSettings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventBannerImageUrl",
                table: "SiteSettings",
                type: "character varying(400)",
                maxLength: 400,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EventBannerLinkUrl",
                table: "SiteSettings",
                type: "character varying(400)",
                maxLength: 400,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EventBannerStartsAt",
                table: "SiteSettings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventBannerTitle",
                table: "SiteSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventBannerEnabled",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EventBannerEndsAt",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EventBannerImageUrl",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EventBannerLinkUrl",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EventBannerStartsAt",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EventBannerTitle",
                table: "SiteSettings");
        }
    }
}
