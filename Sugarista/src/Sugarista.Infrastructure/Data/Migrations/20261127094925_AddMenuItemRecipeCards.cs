using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemRecipeCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MenuItemRecipeCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BulletPointsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false, defaultValue: "[]"),
                    ServingTemp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    GlasswareOrPlate = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PrepSeconds = table.Column<int>(type: "integer", nullable: true),
                    BaristaTip = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemRecipeCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItemRecipeCards_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemRecipeCards_MenuItemId",
                table: "MenuItemRecipeCards",
                column: "MenuItemId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuItemRecipeCards");
        }
    }
}
