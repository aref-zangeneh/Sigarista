using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierPhoneNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "PhoneNumbers",
                table: "Suppliers",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            // Backfill: suppliers created before this change keep their single
            // phone as the first entry of the new list. Purely additive.
            migrationBuilder.Sql(
                "UPDATE \"Suppliers\" SET \"PhoneNumbers\" = ARRAY[\"PhoneNumber\"] " +
                "WHERE COALESCE(\"PhoneNumber\", '') <> '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhoneNumbers",
                table: "Suppliers");
        }
    }
}
