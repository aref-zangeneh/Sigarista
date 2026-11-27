using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffTaskActivityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaskTitleSnapshot = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    AssignedToDisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActorAdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorDisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Details = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffTaskActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    AssignedToAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    RecurrenceType = table.Column<int>(type: "integer", nullable: false),
                    DaysOfWeekMask = table.Column<int>(type: "integer", nullable: false),
                    OneTimeDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RaiseTimeOfDay = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    RequireNoteOnComplete = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffTasks_AdminUsers_AssignedToAdminUserId",
                        column: x => x.AssignedToAdminUserId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffTaskCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurrenceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ScheduledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedByAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedByDisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DelayMinutes = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffTaskCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffTaskCompletions_StaffTasks_StaffTaskId",
                        column: x => x.StaffTaskId,
                        principalTable: "StaffTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffTaskActivityLogs_OccurredAtUtc",
                table: "StaffTaskActivityLogs",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTaskActivityLogs_StaffTaskId",
                table: "StaffTaskActivityLogs",
                column: "StaffTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTaskCompletions_CompletedAtUtc",
                table: "StaffTaskCompletions",
                column: "CompletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTaskCompletions_CompletedByAdminUserId",
                table: "StaffTaskCompletions",
                column: "CompletedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTaskCompletions_StaffTaskId_OccurrenceDate",
                table: "StaffTaskCompletions",
                columns: new[] { "StaffTaskId", "OccurrenceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffTasks_AssignedToAdminUserId_IsActive",
                table: "StaffTasks",
                columns: new[] { "AssignedToAdminUserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffTasks_IsActive_RaiseTimeOfDay",
                table: "StaffTasks",
                columns: new[] { "IsActive", "RaiseTimeOfDay" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffTaskActivityLogs");

            migrationBuilder.DropTable(
                name: "StaffTaskCompletions");

            migrationBuilder.DropTable(
                name: "StaffTasks");
        }
    }
}
