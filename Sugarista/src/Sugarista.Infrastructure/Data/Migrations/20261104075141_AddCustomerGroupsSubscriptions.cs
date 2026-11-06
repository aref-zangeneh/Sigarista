using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerGroupsSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PersonalCupCategoryIds",
                table: "SiteSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalCupDeliveryNote",
                table: "SiteSettings",
                type: "text",
                nullable: false,
                defaultValue: "لیوان شخصی را هنگام تحویل به باریستا نشان دهید.");

            migrationBuilder.AddColumn<decimal>(
                name: "PersonalCupDiscountAmount",
                table: "SiteSettings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "PersonalCupDiscountEnabled",
                table: "SiteSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionCancelRefundPolicy",
                table: "SiteSettings",
                type: "text",
                nullable: false,
                defaultValue: "لغو اشتراک با درخواست مشتری انجام می‌شود؛ معادل استفاده‌های باقی‌مانده به کیف پول مشتری برگشت می‌خورد.");

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionRenewalPolicy",
                table: "SiteSettings",
                type: "text",
                nullable: false,
                defaultValue: "تمدید اشتراک از طریق پیشخوان کافه یا پنل مشتری انجام می‌شود؛ باقی‌ماندهٔ استفاده‌ها به دورهٔ بعد منتقل نمی‌شود مگر با تایید مدیریت.");

            migrationBuilder.CreateTable(
                name: "CustomerGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ApprovalMethod = table.Column<int>(type: "integer", nullable: false),
                    EmailDomain = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    MembershipValidityDays = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TotalUseCount = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValidityDays = table.Column<int>(type: "integer", nullable: false),
                    DailyUseLimit = table.Column<int>(type: "integer", nullable: false),
                    AllowedCategoryIds = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPackages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerGroupMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerGroupMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerGroupMemberships_CustomerGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "CustomerGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerGroupMemberships_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TotalUseCount = table.Column<int>(type: "integer", nullable: false),
                    UsedCount = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DailyUseLimit = table.Column<int>(type: "integer", nullable: false),
                    AllowedCategoryIds = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UsesToday = table.Column<int>(type: "integer", nullable: false),
                    LastUseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerSubscriptions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerSubscriptions_SubscriptionPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "SubscriptionPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerGroupMemberships_CustomerId_GroupId",
                table: "CustomerGroupMemberships",
                columns: new[] { "CustomerId", "GroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerGroupMemberships_GroupId",
                table: "CustomerGroupMemberships",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerGroups_DisplayOrder",
                table: "CustomerGroups",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerGroups_Name",
                table: "CustomerGroups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerSubscriptions_CustomerId",
                table: "CustomerSubscriptions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerSubscriptions_CustomerId_Status",
                table: "CustomerSubscriptions",
                columns: new[] { "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerSubscriptions_ExpiresAt",
                table: "CustomerSubscriptions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerSubscriptions_PackageId",
                table: "CustomerSubscriptions",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPackages_DisplayOrder",
                table: "SubscriptionPackages",
                column: "DisplayOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerGroupMemberships");

            migrationBuilder.DropTable(
                name: "CustomerSubscriptions");

            migrationBuilder.DropTable(
                name: "CustomerGroups");

            migrationBuilder.DropTable(
                name: "SubscriptionPackages");

            migrationBuilder.DropColumn(
                name: "PersonalCupCategoryIds",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PersonalCupDeliveryNote",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PersonalCupDiscountAmount",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PersonalCupDiscountEnabled",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "SubscriptionCancelRefundPolicy",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "SubscriptionRenewalPolicy",
                table: "SiteSettings");
        }
    }
}
