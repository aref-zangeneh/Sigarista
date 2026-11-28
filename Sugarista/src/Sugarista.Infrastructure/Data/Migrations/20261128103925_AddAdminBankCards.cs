using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminBankCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminBankCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BankName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CardHolderName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CardNumber = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ShebaNumber = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    CardImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CardTheme = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "ocean"),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminBankCards", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminBankCards_DisplayOrder",
                table: "AdminBankCards",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_AdminBankCards_IsActive",
                table: "AdminBankCards",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AdminBankCards_IsDefault",
                table: "AdminBankCards",
                column: "IsDefault");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminBankCards");
        }
    }
}
