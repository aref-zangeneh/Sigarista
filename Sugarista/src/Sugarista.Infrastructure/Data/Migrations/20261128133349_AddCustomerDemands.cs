using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerDemands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerDemands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedItem = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestedCount = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "POS"),
                    CustomerName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RecordedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerDemands", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDemands_Category",
                table: "CustomerDemands",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDemands_NormalizedKey",
                table: "CustomerDemands",
                column: "NormalizedKey");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDemands_RequestedAt",
                table: "CustomerDemands",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDemands_Status",
                table: "CustomerDemands",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerDemands");
        }
    }
}
