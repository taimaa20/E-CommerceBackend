using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixPaymentAdjustmentPendingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ApprovedByUserName",
                table: "PaymentAdjustments",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ApprovedAt",
                table: "PaymentAdjustments",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "DecisionNotes",
                table: "PaymentAdjustments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "NewPaymentId",
                table: "PaymentAdjustmentDetails",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "NewPaymentMethodCode",
                table: "PaymentAdjustmentDetails",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewReferenceNumber",
                table: "PaymentAdjustmentDetails",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdjustments_Tenant_Order_Status",
                table: "PaymentAdjustments",
                columns: new[] { "TenantId", "OrderId", "Status" },
                unique: true,
                filter: "\"Status\" = 0 AND \"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentAdjustments_Tenant_Order_Status",
                table: "PaymentAdjustments");

            migrationBuilder.DropColumn(
                name: "DecisionNotes",
                table: "PaymentAdjustments");

            migrationBuilder.DropColumn(
                name: "NewPaymentMethodCode",
                table: "PaymentAdjustmentDetails");

            migrationBuilder.DropColumn(
                name: "NewReferenceNumber",
                table: "PaymentAdjustmentDetails");

            migrationBuilder.AlterColumn<string>(
                name: "ApprovedByUserName",
                table: "PaymentAdjustments",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ApprovedAt",
                table: "PaymentAdjustments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "NewPaymentId",
                table: "PaymentAdjustmentDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
