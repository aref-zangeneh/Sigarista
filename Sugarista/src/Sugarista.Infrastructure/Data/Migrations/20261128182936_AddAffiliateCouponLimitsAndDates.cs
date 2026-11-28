using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAffiliateCouponLimitsAndDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "AffiliateCoupons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxTotalUsages",
                table: "AffiliateCoupons",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartsAt",
                table: "AffiliateCoupons",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "AffiliateCoupons");

            migrationBuilder.DropColumn(
                name: "MaxTotalUsages",
                table: "AffiliateCoupons");

            migrationBuilder.DropColumn(
                name: "StartsAt",
                table: "AffiliateCoupons");
        }
    }
}
