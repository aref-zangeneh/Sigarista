using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBackupSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackupCloudTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    EndpointUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    BucketName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Region = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    AccessKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SecretKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastTestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastTestSuccess = table.Column<bool>(type: "boolean", nullable: true),
                    LastTestMessage = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupCloudTargets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BackupCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: true),
                    JobName = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    TriggerType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Checksum = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    DatabaseEngine = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CloudTargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CloudTargetName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CloudStatus = table.Column<int>(type: "integer", nullable: false),
                    CloudObjectKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CloudUploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CloudErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    ScheduleType = table.Column<int>(type: "integer", nullable: false),
                    IntervalHours = table.Column<int>(type: "integer", nullable: false),
                    RunHourTehran = table.Column<int>(type: "integer", nullable: false),
                    RunMinuteTehran = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: true),
                    RetentionCount = table.Column<int>(type: "integer", nullable: false),
                    CloudTargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastRunStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackupJobs_BackupCloudTargets_CloudTargetId",
                        column: x => x.CloudTargetId,
                        principalTable: "BackupCloudTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupJobs_CloudTargetId",
                table: "BackupJobs",
                column: "CloudTargetId");

            migrationBuilder.CreateIndex(
                name: "IX_BackupJobs_IsEnabled",
                table: "BackupJobs",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_BackupJobs_NextRunAt",
                table: "BackupJobs",
                column: "NextRunAt");

            migrationBuilder.CreateIndex(
                name: "IX_BackupRecords_JobId",
                table: "BackupRecords",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_BackupRecords_StartedAt",
                table: "BackupRecords",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BackupRecords_Status",
                table: "BackupRecords",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupJobs");

            migrationBuilder.DropTable(
                name: "BackupRecords");

            migrationBuilder.DropTable(
                name: "BackupCloudTargets");
        }
    }
}
