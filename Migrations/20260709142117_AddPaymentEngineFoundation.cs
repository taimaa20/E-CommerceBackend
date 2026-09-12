using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentEngineFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "PaymentEngine");

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "PaymentEngine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RequestedCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    AuthorizedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AuthorizedCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CapturedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CapturedCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RefundedCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IdempotencyKeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RequestFingerprintHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_PaymentEngine_Payments_Amount_Caps", "\"CapturedAmount\" <= \"RequestedAmount\" AND \"RefundedAmount\" <= \"CapturedAmount\"");
                    table.CheckConstraint("CK_PaymentEngine_Payments_Amount_Positive", "\"RequestedAmount\" > 0");
                    table.CheckConstraint("CK_PaymentEngine_Payments_Amounts_NonNegative", "\"AuthorizedAmount\" >= 0 AND \"CapturedAmount\" >= 0 AND \"RefundedAmount\" >= 0");
                    table.CheckConstraint("CK_PaymentEngine_Payments_Currency_Consistent", "\"RequestedCurrency\" = \"AuthorizedCurrency\" AND \"RequestedCurrency\" = \"CapturedCurrency\" AND \"RequestedCurrency\" = \"RefundedCurrency\"");
                    table.CheckConstraint("CK_PaymentEngine_Payments_Provider_Normalized", "\"ProviderCode\" = lower(btrim(\"ProviderCode\")) AND length(\"ProviderCode\") > 0");
                    table.CheckConstraint("CK_PaymentEngine_Payments_Status", "\"Status\" BETWEEN 0 AND 9");
                    table.ForeignKey(
                        name: "FK_PaymentEngine_Payments_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentEngine_Payments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAttempts",
                schema: "PaymentEngine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Operation = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    GatewayProviderCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    GatewayReference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ProviderCorrelationId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    FailureReason = table.Column<int>(type: "integer", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetryAfterUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ResponseHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAttempts", x => x.Id);
                    table.CheckConstraint("CK_PaymentEngine_PaymentAttempts_Amount_Positive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_PaymentEngine_PaymentAttempts_AttemptNumber", "\"AttemptNumber\" > 0");
                    table.CheckConstraint("CK_PaymentEngine_PaymentAttempts_FailureReason", "\"FailureReason\" BETWEEN 0 AND 9");
                    table.CheckConstraint("CK_PaymentEngine_PaymentAttempts_Status", "\"Status\" BETWEEN 0 AND 7");
                    table.ForeignKey(
                        name: "FK_PaymentEngine_PaymentAttempts_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "PaymentEngine",
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentSessions",
                schema: "PaymentEngine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SessionProviderCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    SessionReference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CheckoutUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentSessions", x => x.Id);
                    table.CheckConstraint("CK_PaymentEngine_PaymentSessions_CheckoutUrl_Https", "\"CheckoutUrl\" IS NULL OR \"CheckoutUrl\" LIKE 'https://%'");
                    table.CheckConstraint("CK_PaymentEngine_PaymentSessions_Provider_Normalized", "\"ProviderCode\" = lower(btrim(\"ProviderCode\")) AND length(\"ProviderCode\") > 0");
                    table.CheckConstraint("CK_PaymentEngine_PaymentSessions_Status", "\"Status\" BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_PaymentEngine_PaymentSessions_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "PaymentEngine",
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentEvents",
                schema: "PaymentEngine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentAttemptId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EventName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    GatewayProviderCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    GatewayReference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ExternalEventId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEvents", x => x.Id);
                    table.CheckConstraint("CK_PaymentEngine_PaymentEvents_EventName", "length(btrim(\"EventName\")) > 0");
                    table.CheckConstraint("CK_PaymentEngine_PaymentEvents_Provider_Normalized", "\"ProviderCode\" = lower(btrim(\"ProviderCode\")) AND length(\"ProviderCode\") > 0");
                    table.ForeignKey(
                        name: "FK_PaymentEngine_PaymentEvents_PaymentAttempts_PaymentAttemptId",
                        column: x => x.PaymentAttemptId,
                        principalSchema: "PaymentEngine",
                        principalTable: "PaymentAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentEngine_PaymentEvents_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "PaymentEngine",
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEngine_PaymentAttempts_Tenant_Payment_Operation",
                schema: "PaymentEngine",
                table: "PaymentAttempts",
                columns: new[] { "TenantId", "PaymentId", "Operation" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEngine_PaymentAttempts_Tenant_Status_StartedAt",
                schema: "PaymentEngine",
                table: "PaymentAttempts",
                columns: new[] { "TenantId", "Status", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PaymentEngine_PaymentAttempts_GatewayReference",
                schema: "PaymentEngine",
                table: "PaymentAttempts",
                columns: new[] { "GatewayProviderCode", "GatewayReference" },
                unique: true,
                filter: "\"GatewayReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentEngine_PaymentAttempts_Payment_AttemptNumber",
                schema: "PaymentEngine",
                table: "PaymentAttempts",
                columns: new[] { "PaymentId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEngine_PaymentEvents_PaymentAttemptId",
                schema: "PaymentEngine",
                table: "PaymentEvents",
                column: "PaymentAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEngine_PaymentEvents_PaymentId",
                schema: "PaymentEngine",
                table: "PaymentEvents",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEngine_PaymentEvents_Tenant_EventName_OccurredAt",
                schema: "PaymentEngine",
                table: "PaymentEvents",
                columns: new[] { "TenantId", "EventName", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEngine_PaymentEvents_Tenant_PayloadHash",
                schema: "PaymentEngine",
                table: "PaymentEvents",
                columns: new[] { "TenantId", "PayloadHash" },
                filter: "\"PayloadHash\" IS NOT NULL AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEngine_PaymentEvents_Tenant_Payment_OccurredAt",
                schema: "PaymentEngine",
                table: "PaymentEvents",
                columns: new[] { "TenantId", "PaymentId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PaymentEngine_PaymentEvents_Tenant_Provider_ExternalEvent",
                schema: "PaymentEngine",
                table: "PaymentEvents",
                columns: new[] { "TenantId", "ProviderCode", "ExternalEventId" },
                unique: true,
                filter: "\"ExternalEventId\" IS NOT NULL AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEngine_Payments_BranchId",
                schema: "PaymentEngine",
                table: "Payments",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEngine_Payments_OrderId",
                schema: "PaymentEngine",
                table: "Payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEngine_Payments_Tenant_Order",
                schema: "PaymentEngine",
                table: "Payments",
                columns: new[] { "TenantId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEngine_Payments_Tenant_Provider_Status",
                schema: "PaymentEngine",
                table: "Payments",
                columns: new[] { "TenantId", "ProviderCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEngine_Payments_Tenant_Status_UpdatedAt",
                schema: "PaymentEngine",
                table: "Payments",
                columns: new[] { "TenantId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_PaymentEngine_Payments_Tenant_IdempotencyKey",
                schema: "PaymentEngine",
                table: "Payments",
                columns: new[] { "TenantId", "IdempotencyKeyHash" },
                unique: true,
                filter: "\"IdempotencyKeyHash\" IS NOT NULL AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentEngine_Payments_Tenant_MerchantReference",
                schema: "PaymentEngine",
                table: "Payments",
                columns: new[] { "TenantId", "MerchantReference" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEngine_PaymentSessions_Tenant_Provider_Status",
                schema: "PaymentEngine",
                table: "PaymentSessions",
                columns: new[] { "TenantId", "ProviderCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEngine_PaymentSessions_Tenant_Status_ExpiresAt",
                schema: "PaymentEngine",
                table: "PaymentSessions",
                columns: new[] { "TenantId", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PaymentEngine_PaymentSessions_Active_PerPayment",
                schema: "PaymentEngine",
                table: "PaymentSessions",
                column: "PaymentId",
                unique: true,
                filter: "\"Status\" IN (0, 1, 2) AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentEngine_PaymentSessions_SessionReference",
                schema: "PaymentEngine",
                table: "PaymentSessions",
                columns: new[] { "SessionProviderCode", "SessionReference" },
                unique: true,
                filter: "\"SessionReference\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentEvents",
                schema: "PaymentEngine");

            migrationBuilder.DropTable(
                name: "PaymentSessions",
                schema: "PaymentEngine");

            migrationBuilder.DropTable(
                name: "PaymentAttempts",
                schema: "PaymentEngine");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "PaymentEngine");
        }
    }
}
