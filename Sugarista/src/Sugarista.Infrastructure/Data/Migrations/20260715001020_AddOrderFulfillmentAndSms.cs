using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sugarista.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderFulfillmentAndSms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FulfillmentStatus",
                table: "Orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FulfillmentUpdatedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReadyAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReadySmsSentAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_FulfillmentStatus",
                table: "Orders",
                column: "FulfillmentStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_FulfillmentStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentUpdatedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReadyAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReadySmsSentAt",
                table: "Orders");
        }
    }
}
