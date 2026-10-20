using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sugarista.Infrastructure.Data;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261021000000_AddExportJobs")]
    /// <inheritdoc />
    public partial class AddExportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExportJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RequestedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    StoragePath = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1020)", maxLength: 1020, nullable: true),
                    RowsCount = table.Column<long>(type: "bigint", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExportJobs_Status",
                table: "ExportJobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExportJobs");
        }
    }
}
