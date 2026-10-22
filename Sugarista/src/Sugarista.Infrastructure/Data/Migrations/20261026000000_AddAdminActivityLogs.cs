using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261026000000_AddAdminActivityLogs")]
    /// <inheritdoc />
    public partial class AddAdminActivityLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminActivityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActorDisplayName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SectionId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SectionTitle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ChangedFields = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HttpMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminActivityLogs_OccurredAt",
                table: "AdminActivityLogs",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminActivityLogs_ActorUsername_OccurredAt",
                table: "AdminActivityLogs",
                columns: new[] { "ActorUsername", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminActivityLogs_SectionId_OccurredAt",
                table: "AdminActivityLogs",
                columns: new[] { "SectionId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminActivityLogs");
        }
    }
}
