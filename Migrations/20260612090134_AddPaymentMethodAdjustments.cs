using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentMethodAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedByUserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedByUserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAdjustments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentAdjustments_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaymentAdjustments_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAdjustmentDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdjustmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    NewPaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldPaymentMethodId = table.Column<Guid>(type: "uuid", nullable: true),
                    NewPaymentMethodId = table.Column<Guid>(type: "uuid", nullable: true),
                    OldPaymentMethodName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OldPaymentMethodNameAr = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    NewPaymentMethodName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NewPaymentMethodNameAr = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAdjustmentDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAdjustmentDetails_PaymentAdjustments_AdjustmentId",
                        column: x => x.AdjustmentId,
                        principalTable: "PaymentAdjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentAdjustmentDetails_PaymentMethods_NewPaymentMethodId",
                        column: x => x.NewPaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaymentAdjustmentDetails_PaymentMethods_OldPaymentMethodId",
                        column: x => x.OldPaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaymentAdjustmentDetails_Payments_NewPaymentId",
                        column: x => x.NewPaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentAdjustmentDetails_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdjustmentDetails_AdjustmentId",
                table: "PaymentAdjustmentDetails",
                column: "AdjustmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdjustmentDetails_NewPaymentId",
                table: "PaymentAdjustmentDetails",
                column: "NewPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdjustmentDetails_NewPaymentMethodId",
                table: "PaymentAdjustmentDetails",
                column: "NewPaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdjustmentDetails_OldPaymentMethodId",
                table: "PaymentAdjustmentDetails",
                column: "OldPaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdjustmentDetails_PaymentId",
                table: "PaymentAdjustmentDetails",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdjustmentDetails_Tenant_Adjustment",
                table: "PaymentAdjustmentDetails",
                columns: new[] { "TenantId", "AdjustmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdjustmentDetails_Tenant_Methods",
                table: "PaymentAdjustmentDetails",
                columns: new[] { "TenantId", "OldPaymentMethodId", "NewPaymentMethodId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdjustments_ApprovedByUserId",
                table: "PaymentAdjustments",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdjustments_OrderId",
                table: "PaymentAdjustments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdjustments_RequestedByUserId",
                table: "PaymentAdjustments",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdjustments_Tenant_ApprovedAt",
                table: "PaymentAdjustments",
                columns: new[] { "TenantId", "ApprovedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdjustments_Tenant_Order_ApprovedAt",
                table: "PaymentAdjustments",
                columns: new[] { "TenantId", "OrderId", "ApprovedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentAdjustmentDetails");

            migrationBuilder.DropTable(
                name: "PaymentAdjustments");
        }
    }
}
