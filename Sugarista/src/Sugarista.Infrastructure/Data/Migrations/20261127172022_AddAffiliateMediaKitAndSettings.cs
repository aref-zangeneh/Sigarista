using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAffiliateMediaKitAndSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AffiliateDefaultCreditBonusPercent",
                table: "SiteSettings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AffiliateMinPayoutAmount",
                table: "SiteSettings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "AffiliateMediaAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssetType = table.Column<int>(type: "integer", nullable: false),
                    FileUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CaptionText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateMediaAssets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateMediaAssets_IsActive_DisplayOrder",
                table: "AffiliateMediaAssets",
                columns: new[] { "IsActive", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AffiliateMediaAssets");

            migrationBuilder.DropColumn(
                name: "AffiliateDefaultCreditBonusPercent",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AffiliateMinPayoutAmount",
                table: "SiteSettings");
        }
    }
}
