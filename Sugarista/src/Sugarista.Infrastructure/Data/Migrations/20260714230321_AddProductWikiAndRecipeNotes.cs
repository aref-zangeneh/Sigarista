using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductWikiAndRecipeNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreparationNote",
                table: "RecipeItems",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductWikiGuides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Overview = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    QualityStandards = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EquipmentNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ServingNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductWikiGuides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductWikiGuides_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductWikiSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Instructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    IsQualityCheckpoint = table.Column<bool>(type: "boolean", nullable: false),
                    ProductWikiGuideId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductWikiSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductWikiSteps_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductWikiSteps_ProductWikiGuides_ProductWikiGuideId",
                        column: x => x.ProductWikiGuideId,
                        principalTable: "ProductWikiGuides",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductWikiGuides_MenuItemId",
                table: "ProductWikiGuides",
                column: "MenuItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductWikiSteps_MenuItemId_StepOrder",
                table: "ProductWikiSteps",
                columns: new[] { "MenuItemId", "StepOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductWikiSteps_ProductWikiGuideId",
                table: "ProductWikiSteps",
                column: "ProductWikiGuideId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductWikiSteps");

            migrationBuilder.DropTable(
                name: "ProductWikiGuides");

            migrationBuilder.DropColumn(
                name: "PreparationNote",
                table: "RecipeItems");
        }
    }
}
