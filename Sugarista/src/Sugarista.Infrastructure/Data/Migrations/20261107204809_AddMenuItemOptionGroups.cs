using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemOptionGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "OptionLabels",
                table: "OrderLines",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "OptionsJson",
                table: "OrderLines",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MenuItemOptionGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersianName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    EnglishName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SelectionMode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MinSelections = table.Column<int>(type: "integer", nullable: false),
                    MaxSelections = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemOptionGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItemOptionGroups_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MenuItemOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersianName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    EnglishName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    PriceDelta = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItemOptions_MenuItemOptionGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "MenuItemOptionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MenuItemOptionIngredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemOptionIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItemOptionIngredients_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuItemOptionIngredients_MenuItemOptions_OptionId",
                        column: x => x.OptionId,
                        principalTable: "MenuItemOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemOptionGroups_MenuItemId_DisplayOrder",
                table: "MenuItemOptionGroups",
                columns: new[] { "MenuItemId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemOptionIngredients_IngredientId",
                table: "MenuItemOptionIngredients",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemOptionIngredients_OptionId_IngredientId",
                table: "MenuItemOptionIngredients",
                columns: new[] { "OptionId", "IngredientId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemOptions_GroupId_DisplayOrder",
                table: "MenuItemOptions",
                columns: new[] { "GroupId", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuItemOptionIngredients");

            migrationBuilder.DropTable(
                name: "MenuItemOptions");

            migrationBuilder.DropTable(
                name: "MenuItemOptionGroups");

            migrationBuilder.DropColumn(
                name: "OptionLabels",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "OptionsJson",
                table: "OrderLines");
        }
    }
}
