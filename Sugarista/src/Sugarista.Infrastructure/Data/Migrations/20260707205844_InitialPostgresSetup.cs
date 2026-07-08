using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgresSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiteSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CafeName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    BrowserTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MetaDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Eyebrow = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    BrandTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BrandSubtitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    HeroBadgeText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HeroTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    HeroDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    HeroPrimaryButtonLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HeroSecondaryButtonLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HeroHours = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HowToOrderLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HowToOrderTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HowToOrderStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StepOneTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StepOneDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StepTwoTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StepTwoDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StepThreeTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StepThreeDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    QuickAccessLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    QuickAccessTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    QuickAccessDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PopularLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PopularTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PopularDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MenuLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MenuTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MenuToolbarHint = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SearchPlaceholder = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VisitLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VisitTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VisitDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BreakfastHours = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TakeawayStatus = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    InstagramUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FooterText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FooterPopularLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FooterMenuLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FooterCallLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MenuItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Price = table.Column<int>(type: "integer", nullable: false),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    Badge = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Featured = table.Column<bool>(type: "boolean", nullable: false),
                    Accent = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ShortLabel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItems_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminUsers_Username",
                table: "AdminUsers",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_CategoryId",
                table: "MenuItems",
                column: "CategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminUsers");

            migrationBuilder.DropTable(
                name: "MenuItems");

            migrationBuilder.DropTable(
                name: "SiteSettings");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
