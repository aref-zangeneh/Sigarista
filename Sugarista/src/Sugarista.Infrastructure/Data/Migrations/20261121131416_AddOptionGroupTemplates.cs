using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionGroupTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceTemplateId",
                table: "MenuItemOptionGroups",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OptionGroupTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PersianName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    EnglishName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SelectionMode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MinSelections = table.Column<int>(type: "integer", nullable: false),
                    MaxSelections = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptionGroupTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OptionGroupTemplateItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersianName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    EnglishName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    PriceDelta = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptionGroupTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OptionGroupTemplateItems_OptionGroupTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "OptionGroupTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OptionGroupTemplateItemIngredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptionGroupTemplateItemIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OptionGroupTemplateItemIngredients_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OptionGroupTemplateItemIngredients_OptionGroupTemplateItems~",
                        column: x => x.TemplateItemId,
                        principalTable: "OptionGroupTemplateItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemOptionGroups_SourceTemplateId",
                table: "MenuItemOptionGroups",
                column: "SourceTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_OptionGroupTemplateItemIngredients_IngredientId",
                table: "OptionGroupTemplateItemIngredients",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_OptionGroupTemplateItemIngredients_TemplateItemId_Ingredien~",
                table: "OptionGroupTemplateItemIngredients",
                columns: new[] { "TemplateItemId", "IngredientId" });

            migrationBuilder.CreateIndex(
                name: "IX_OptionGroupTemplateItems_TemplateId_DisplayOrder",
                table: "OptionGroupTemplateItems",
                columns: new[] { "TemplateId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_OptionGroupTemplates_CategoryLabel_DisplayOrder",
                table: "OptionGroupTemplates",
                columns: new[] { "CategoryLabel", "DisplayOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItemOptionGroups_OptionGroupTemplates_SourceTemplateId",
                table: "MenuItemOptionGroups",
                column: "SourceTemplateId",
                principalTable: "OptionGroupTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuItemOptionGroups_OptionGroupTemplates_SourceTemplateId",
                table: "MenuItemOptionGroups");

            migrationBuilder.DropTable(
                name: "OptionGroupTemplateItemIngredients");

            migrationBuilder.DropTable(
                name: "OptionGroupTemplateItems");

            migrationBuilder.DropTable(
                name: "OptionGroupTemplates");

            migrationBuilder.DropIndex(
                name: "IX_MenuItemOptionGroups_SourceTemplateId",
                table: "MenuItemOptionGroups");

            migrationBuilder.DropColumn(
                name: "SourceTemplateId",
                table: "MenuItemOptionGroups");
        }
    }
}
