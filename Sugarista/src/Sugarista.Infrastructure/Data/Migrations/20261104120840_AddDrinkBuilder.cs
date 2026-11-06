using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDrinkBuilder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CustomDrinkBuilderEnabled",
                table: "SiteSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string[]>(
                name: "CustomDrinkSummary",
                table: "OrderLines",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "CustomizationJson",
                table: "OrderLines",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DrinkBuilderProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrinkBuilderProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DrinkBuilderProfiles_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DrinkBuilderGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    InputType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SelectionMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MinSelections = table.Column<int>(type: "integer", nullable: false),
                    MaxSelections = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrinkBuilderGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DrinkBuilderGroups_DrinkBuilderProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "DrinkBuilderProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DrinkBuilderOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    EnglishName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PricePerUnit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MinAmount = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    MaxAmount = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    Step = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    DefaultAmount = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrinkBuilderOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DrinkBuilderOptions_DrinkBuilderGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "DrinkBuilderGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DrinkBuilderGroups_ProfileId_DisplayOrder",
                table: "DrinkBuilderGroups",
                columns: new[] { "ProfileId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DrinkBuilderOptions_GroupId_DisplayOrder",
                table: "DrinkBuilderOptions",
                columns: new[] { "GroupId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DrinkBuilderProfiles_MenuItemId",
                table: "DrinkBuilderProfiles",
                column: "MenuItemId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DrinkBuilderOptions");

            migrationBuilder.DropTable(
                name: "DrinkBuilderGroups");

            migrationBuilder.DropTable(
                name: "DrinkBuilderProfiles");

            migrationBuilder.DropColumn(
                name: "CustomDrinkBuilderEnabled",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "CustomDrinkSummary",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "CustomizationJson",
                table: "OrderLines");
        }
    }
}
