using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAffiliateSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AffiliateTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MinMonthlySales = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CreditBonusPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateTiers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CategoryCommissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommissionPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryCommissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryCommissions_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Affiliates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ShebaNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CardNumber = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    CashBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WalletCreditBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CustomCommissionRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Affiliates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Affiliates_AffiliateTiers_TierId",
                        column: x => x.TierId,
                        principalTable: "AffiliateTiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Affiliates_Customers_UserId",
                        column: x => x.UserId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateCommissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderSource = table.Column<int>(type: "integer", nullable: false),
                    OrderTotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EligibleAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateCommissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliateCommissions_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AffiliateCommissions_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateCoupons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DiscountType = table.Column<int>(type: "integer", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxDiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsFirstOrderOnly = table.Column<bool>(type: "boolean", nullable: false),
                    UsageLimitPerUser = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateCoupons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliateCoupons_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SubId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TargetUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ClickCount = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliateLinks_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AffiliatePayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PayoutType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TrackingCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ReceiptImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliatePayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliatePayouts_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReferralClicks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackingToken = table.Column<Guid>(type: "uuid", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralClicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferralClicks_AffiliateLinks_LinkId",
                        column: x => x.LinkId,
                        principalTable: "AffiliateLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissions_AffiliateId_OrderId",
                table: "AffiliateCommissions",
                columns: new[] { "AffiliateId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissions_AffiliateId_Status_CreatedAt",
                table: "AffiliateCommissions",
                columns: new[] { "AffiliateId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissions_OrderId",
                table: "AffiliateCommissions",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCoupons_AffiliateId",
                table: "AffiliateCoupons",
                column: "AffiliateId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCoupons_Code",
                table: "AffiliateCoupons",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCoupons_IsActive",
                table: "AffiliateCoupons",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateLinks_AffiliateId",
                table: "AffiliateLinks",
                column: "AffiliateId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateLinks_Slug",
                table: "AffiliateLinks",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateLinks_Slug_SubId",
                table: "AffiliateLinks",
                columns: new[] { "Slug", "SubId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliatePayouts_AffiliateId",
                table: "AffiliatePayouts",
                column: "AffiliateId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliatePayouts_ProcessedAt",
                table: "AffiliatePayouts",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliatePayouts_Status",
                table: "AffiliatePayouts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliatePayouts_TrackingCode",
                table: "AffiliatePayouts",
                column: "TrackingCode");

            migrationBuilder.CreateIndex(
                name: "IX_Affiliates_Status",
                table: "Affiliates",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Affiliates_TierId",
                table: "Affiliates",
                column: "TierId");

            migrationBuilder.CreateIndex(
                name: "IX_Affiliates_UserId",
                table: "Affiliates",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateTiers_MinMonthlySales",
                table: "AffiliateTiers",
                column: "MinMonthlySales");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateTiers_Name",
                table: "AffiliateTiers",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoryCommissions_CategoryId",
                table: "CategoryCommissions",
                column: "CategoryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralClicks_CreatedAt",
                table: "ReferralClicks",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralClicks_ExpiresAt",
                table: "ReferralClicks",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralClicks_LinkId",
                table: "ReferralClicks",
                column: "LinkId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralClicks_TrackingToken",
                table: "ReferralClicks",
                column: "TrackingToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AffiliateCommissions");

            migrationBuilder.DropTable(
                name: "AffiliateCoupons");

            migrationBuilder.DropTable(
                name: "AffiliatePayouts");

            migrationBuilder.DropTable(
                name: "CategoryCommissions");

            migrationBuilder.DropTable(
                name: "ReferralClicks");

            migrationBuilder.DropTable(
                name: "AffiliateLinks");

            migrationBuilder.DropTable(
                name: "Affiliates");

            migrationBuilder.DropTable(
                name: "AffiliateTiers");
        }
    }
}
