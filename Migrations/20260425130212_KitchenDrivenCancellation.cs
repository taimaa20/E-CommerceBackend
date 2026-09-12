using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class KitchenDrivenCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CashierActionById",
                table: "WasteLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CashierActionByName",
                table: "WasteLogs",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KitchenDecision",
                table: "WasteLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "KitchenDecisionById",
                table: "WasteLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KitchenDecisionByName",
                table: "WasteLogs",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StockDeductedAt",
                table: "OrderItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StockDeductedCost",
                table: "OrderItems",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "CancelLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "KitchenDecidedAt",
                table: "CancelLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KitchenDecision",
                table: "CancelLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "KitchenDecisionById",
                table: "CancelLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KitchenDecisionByName",
                table: "CancelLogs",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresKitchenApproval",
                table: "CancelLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CashierActionById",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "CashierActionByName",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "KitchenDecision",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "KitchenDecisionById",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "KitchenDecisionByName",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "StockDeductedAt",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "StockDeductedCost",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "CancelLogs");

            migrationBuilder.DropColumn(
                name: "KitchenDecidedAt",
                table: "CancelLogs");

            migrationBuilder.DropColumn(
                name: "KitchenDecision",
                table: "CancelLogs");

            migrationBuilder.DropColumn(
                name: "KitchenDecisionById",
                table: "CancelLogs");

            migrationBuilder.DropColumn(
                name: "KitchenDecisionByName",
                table: "CancelLogs");

            migrationBuilder.DropColumn(
                name: "RequiresKitchenApproval",
                table: "CancelLogs");
        }
    }
}
