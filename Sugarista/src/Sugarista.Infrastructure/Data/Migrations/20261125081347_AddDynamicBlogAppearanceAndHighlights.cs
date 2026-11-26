using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicBlogAppearanceAndHighlights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlogSettingsJson",
                table: "SiteSettings",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BlogHighlights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Subtitle = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    BadgeText = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IconText = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LinkUrl = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    LinkLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ThemeStyle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogHighlights", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlogHighlights_DisplayOrder",
                table: "BlogHighlights",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_BlogHighlights_IsActive",
                table: "BlogHighlights",
                column: "IsActive");

            migrationBuilder.Sql("""
                INSERT INTO "BlogHighlights" ("Id", "Title", "Subtitle", "BadgeText", "IconText", "ImageUrl", "LinkUrl", "LinkLabel", "ThemeStyle", "DisplayOrder", "IsActive", "CreatedAt")
                VALUES
                  ('11111111-2222-4333-8444-555555555501', 'راز کرمای طلایی اسپرسو', 'دمای ۹۳ درجه و عصاره‌گیری ۲۸ ثانیه‌ای روی دانه‌های تازه برشته، امضای هر شات اسپرسوی شوگاریستاست.', 'نکته باریستا', '☕', NULL, '/mag', 'مطالعه راهنماها', 'teal', 1, TRUE, NOW()),
                  ('11111111-2222-4333-8444-555555555502', 'قنادی دست‌ساز و پخت روز', 'کیک‌ها، کروسان‌های کره‌ای و دسرهای تخصصی ما هر روز صبح تازه در کارگاه شوگاریستا آماده می‌شوند.', 'هنر قنادی', '🥐', NULL, '/', 'مشاهده شیرینی‌ها در منو', 'magenta', 2, TRUE, NOW()),
                  ('11111111-2222-4333-8444-555555555503', 'جفت‌سازی قهوه و شیرینی', 'چطور بر اساس شیرینی و اسیدیته دسر، بهترین نوشیدنی دمی یا اسپرسو را کنار آن انتخاب کنیم؟', 'پیشنهاد سرآشپز', '✨', NULL, '/mag', 'کشف ترکیب‌های طعمی', 'gold', 3, TRUE, NOW());
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlogHighlights");

            migrationBuilder.DropColumn(
                name: "BlogSettingsJson",
                table: "SiteSettings");
        }
    }
}
