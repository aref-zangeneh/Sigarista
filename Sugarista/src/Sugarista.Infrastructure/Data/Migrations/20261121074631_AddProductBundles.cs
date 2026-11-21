using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBundles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BundleSnapshotJson",
                table: "OrderLines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBundle",
                table: "MenuItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MenuItemBundleItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BundleMenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentMenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SizeKey = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemBundleItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItemBundleItems_MenuItems_BundleMenuItemId",
                        column: x => x.BundleMenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuItemBundleItems_MenuItems_ComponentMenuItemId",
                        column: x => x.ComponentMenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_IsBundle",
                table: "MenuItems",
                column: "IsBundle");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemBundleItems_BundleMenuItemId",
                table: "MenuItemBundleItems",
                column: "BundleMenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemBundleItems_ComponentMenuItemId",
                table: "MenuItemBundleItems",
                column: "ComponentMenuItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuItemBundleItems");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_IsBundle",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "BundleSnapshotJson",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "IsBundle",
                table: "MenuItems");
        }
    }
}
