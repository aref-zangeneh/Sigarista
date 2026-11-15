using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SmsCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "character varying(1400)", maxLength: 1400, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalRecipients = table.Column<int>(type: "integer", nullable: false),
                    SentCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    CancelRequested = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsCampaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmsContactGroupMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsContactGroupMembers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmsContactGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsContactGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmsContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    LastName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Mobile = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MarketingConsent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsContacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmsMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Mobile = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Body = table.Column<string>(type: "character varying(1400)", maxLength: 1400, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    SendType = table.Column<int>(type: "integer", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchNumber = table.Column<int>(type: "integer", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProviderMessageId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ProviderResponse = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Cost = table.Column<decimal>(type: "numeric", nullable: true),
                    Segments = table.Column<int>(type: "integer", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(520)", maxLength: 520, nullable: true),
                    LastStatusAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmsOptOuts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Mobile = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsOptOuts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmsProviderLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    Detail = table.Column<string>(type: "character varying(520)", maxLength: 520, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsProviderLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmsProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProviderType = table.Column<int>(type: "integer", nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    EncryptedApiKey = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    EncryptedUsername = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    EncryptedPassword = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    ExtraSettingsJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LastSuccessfulConnectionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastConnectionError = table.Column<string>(type: "character varying(520)", maxLength: 520, nullable: true),
                    LastConnectionErrorAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmsTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Body = table.Column<string>(type: "character varying(1400)", maxLength: 1400, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmsSenderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IsDefaultLine = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsSenderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmsSenderLines_SmsProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "SmsProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SmsTemplateProviderMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderType = table.Column<int>(type: "integer", nullable: false),
                    ProviderTemplateId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsTemplateProviderMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmsTemplateProviderMappings_SmsTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "SmsTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmsCampaigns_Status",
                table: "SmsCampaigns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SmsContactGroupMembers_ContactId",
                table: "SmsContactGroupMembers",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsContactGroupMembers_GroupId_ContactId",
                table: "SmsContactGroupMembers",
                columns: new[] { "GroupId", "ContactId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsContactGroups_Name",
                table: "SmsContactGroups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsContacts_LastName_FirstName",
                table: "SmsContacts",
                columns: new[] { "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_SmsContacts_Mobile",
                table: "SmsContacts",
                column: "Mobile",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsMessages_CampaignId",
                table: "SmsMessages",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsMessages_Mobile",
                table: "SmsMessages",
                column: "Mobile");

            migrationBuilder.CreateIndex(
                name: "IX_SmsMessages_ProviderMessageId",
                table: "SmsMessages",
                column: "ProviderMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsMessages_Status_RequestedAt",
                table: "SmsMessages",
                columns: new[] { "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SmsOptOuts_Mobile",
                table: "SmsOptOuts",
                column: "Mobile",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsProviderLogs_CreatedAt",
                table: "SmsProviderLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SmsProviderLogs_ProviderId_Action",
                table: "SmsProviderLogs",
                columns: new[] { "ProviderId", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_SmsProviders_ProviderType",
                table: "SmsProviders",
                column: "ProviderType");

            migrationBuilder.CreateIndex(
                name: "IX_SmsSenderLines_ProviderId",
                table: "SmsSenderLines",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsTemplateProviderMappings_TemplateId_ProviderType",
                table: "SmsTemplateProviderMappings",
                columns: new[] { "TemplateId", "ProviderType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmsCampaigns");

            migrationBuilder.DropTable(
                name: "SmsContactGroupMembers");

            migrationBuilder.DropTable(
                name: "SmsContactGroups");

            migrationBuilder.DropTable(
                name: "SmsContacts");

            migrationBuilder.DropTable(
                name: "SmsMessages");

            migrationBuilder.DropTable(
                name: "SmsOptOuts");

            migrationBuilder.DropTable(
                name: "SmsProviderLogs");

            migrationBuilder.DropTable(
                name: "SmsSenderLines");

            migrationBuilder.DropTable(
                name: "SmsTemplateProviderMappings");

            migrationBuilder.DropTable(
                name: "SmsProviders");

            migrationBuilder.DropTable(
                name: "SmsTemplates");
        }
    }
}
