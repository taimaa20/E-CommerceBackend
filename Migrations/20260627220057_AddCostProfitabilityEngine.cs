using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCostProfitabilityEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "WasteDate",
                table: "WasteLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangedFields",
                table: "WasteLogAudits",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewValues",
                table: "WasteLogAudits",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousValues",
                table: "WasteLogAudits",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostSharingCommissionAmount",
                table: "Payments",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostSharingCommissionPercentage",
                table: "Payments",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostSharingCounterpartyPercentage",
                table: "Payments",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostSharingCounterpartyShareAmount",
                table: "Payments",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CostSharingMode",
                table: "Payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CostSharingRestaurantPercentage",
                table: "Payments",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 100m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostSharingRestaurantShareAmount",
                table: "Payments",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CostSharingScope",
                table: "Payments",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "CostSharingCommissionPercentage",
                table: "PaymentMethods",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostSharingCounterpartyPercentage",
                table: "PaymentMethods",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CostSharingMode",
                table: "PaymentMethods",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CostSharingRestaurantPercentage",
                table: "PaymentMethods",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 100m);

            migrationBuilder.AddColumn<int>(
                name: "CostSharingScope",
                table: "PaymentMethods",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "CostSharingCalculatedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostSharingCounterpartyShare",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CostSharingDetailsJson",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostSharingNetSettlement",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostSharingRestaurantShare",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostSharingTotalCommission",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostSharingCommissionPercentage",
                table: "DeliveryPartners",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostSharingCounterpartyPercentage",
                table: "DeliveryPartners",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CostSharingMode",
                table: "DeliveryPartners",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CostSharingRestaurantPercentage",
                table: "DeliveryPartners",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 100m);

            migrationBuilder.AddColumn<int>(
                name: "CostSharingScope",
                table: "DeliveryPartners",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "CostSharingOverrideAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousRuleJson = table.Column<string>(type: "text", nullable: true),
                    NewRuleJson = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostSharingOverrideAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostSharingOverrideAudits_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CostSharingOverrideAudits_Users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CostSharingProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostSharingProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderProfitabilitySnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrderType = table.Column<int>(type: "integer", nullable: false),
                    OrderSource = table.Column<int>(type: "integer", nullable: false),
                    OrderStatus = table.Column<int>(type: "integer", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CashierId = table.Column<Guid>(type: "uuid", nullable: true),
                    CashierName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    DeliveryPartnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryPartnerName = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    DeliveryPartnerNameAr = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    DeliveryPartnerCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PaymentMethodId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentMethodName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PaymentMethodNameAr = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PaymentMethodCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    GrossSales = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Discounts = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ServiceCharges = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Taxes = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NetSales = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ProviderCommission = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RestaurantShare = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ProviderShare = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CardFees = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GatewayFees = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DeliveryFees = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OtherOperationalFees = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalExternalFees = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalRestaurantFees = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GrossRevenue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NetRevenue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    FoodCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PackagingCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DeliveryCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PartnerCommission = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaymentProcessingFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RestaurantCostShare = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ProviderCostShare = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LaborCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OperationalCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GrossProfit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GrossMarginPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    OperatingProfit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OperatingMarginPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    NetProfit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NetMarginPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    ProfitPerOrder = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ContributionMargin = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ContributionPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    CostSharingDetailsJson = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    IsFinalized = table.Column<bool>(type: "boolean", nullable: false),
                    SnapshotAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinalizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderProfitabilitySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderProfitabilitySnapshots_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CostSharingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    ProviderType = table.Column<int>(type: "integer", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryPartnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentMethodId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    CardTypeCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    FeeType = table.Column<int>(type: "integer", nullable: false),
                    FeePercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    FixedFeeAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MinimumFeeAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MaximumFeeAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ApplyVatOnFee = table.Column<bool>(type: "boolean", nullable: false),
                    VatPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    RestaurantPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    CounterpartyPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TierDefinitionJson = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostSharingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostSharingRules_CostSharingProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "CostSharingProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CostSharingRules_DeliveryPartners_DeliveryPartnerId",
                        column: x => x.DeliveryPartnerId,
                        principalTable: "DeliveryPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CostSharingRules_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OrderProfitabilitySnapshotItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductNameAr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    GrossRevenue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    FoodCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CommissionCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CardFeeCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OperationalCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GrossProfit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NetProfit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MarginPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderProfitabilitySnapshotItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderProfitabilitySnapshotItems_OrderProfitabilitySnapshots~",
                        column: x => x.SnapshotId,
                        principalTable: "OrderProfitabilitySnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: new Guid("1ab8e74c-9912-42fa-9c1d-56d6f8f2d501"),
                columns: new[] { "CostSharingRestaurantPercentage", "CostSharingScope" },
                values: new object[] { 100m, 1 });

            migrationBuilder.UpdateData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: new Guid("1ab8e74c-9912-42fa-9c1d-56d6f8f2d502"),
                columns: new[] { "CostSharingRestaurantPercentage", "CostSharingScope" },
                values: new object[] { 100m, 1 });

            migrationBuilder.UpdateData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: new Guid("1ab8e74c-9912-42fa-9c1d-56d6f8f2d503"),
                columns: new[] { "CostSharingRestaurantPercentage", "CostSharingScope" },
                values: new object[] { 100m, 1 });

            migrationBuilder.UpdateData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: new Guid("1ab8e74c-9912-42fa-9c1d-56d6f8f2d504"),
                columns: new[] { "CostSharingRestaurantPercentage", "CostSharingScope" },
                values: new object[] { 100m, 1 });

            migrationBuilder.UpdateData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: new Guid("1ab8e74c-9912-42fa-9c1d-56d6f8f2d505"),
                columns: new[] { "CostSharingRestaurantPercentage", "CostSharingScope" },
                values: new object[] { 100m, 1 });

            migrationBuilder.UpdateData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: new Guid("1ab8e74c-9912-42fa-9c1d-56d6f8f2d506"),
                columns: new[] { "CostSharingRestaurantPercentage", "CostSharingScope" },
                values: new object[] { 100m, 1 });

            migrationBuilder.UpdateData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: new Guid("1ab8e74c-9912-42fa-9c1d-56d6f8f2d507"),
                columns: new[] { "CostSharingRestaurantPercentage", "CostSharingScope" },
                values: new object[] { 100m, 1 });

            migrationBuilder.UpdateData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: new Guid("1ab8e74c-9912-42fa-9c1d-56d6f8f2d508"),
                columns: new[] { "CostSharingRestaurantPercentage", "CostSharingScope" },
                values: new object[] { 100m, 1 });

            migrationBuilder.UpdateData(
                table: "PaymentMethods",
                keyColumn: "Id",
                keyValue: new Guid("1ab8e74c-9912-42fa-9c1d-56d6f8f2d509"),
                columns: new[] { "CostSharingRestaurantPercentage", "CostSharingScope" },
                values: new object[] { 100m, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogs_TenantId_WasteDate",
                table: "WasteLogs",
                columns: new[] { "TenantId", "WasteDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CostSharingOverrideAudits_ApprovedByUserId",
                table: "CostSharingOverrideAudits",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CostSharingOverrideAudits_PerformedByUserId",
                table: "CostSharingOverrideAudits",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CostSharingOverrideAudits_Tenant_Order_Time",
                table: "CostSharingOverrideAudits",
                columns: new[] { "TenantId", "OrderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CostSharingProviders_Tenant_Code",
                table: "CostSharingProviders",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CostSharingProviders_Tenant_Type_Active",
                table: "CostSharingProviders",
                columns: new[] { "TenantId", "Type", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CostSharingRules_DeliveryPartnerId",
                table: "CostSharingRules",
                column: "DeliveryPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CostSharingRules_PaymentMethodId",
                table: "CostSharingRules",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_CostSharingRules_ProviderId",
                table: "CostSharingRules",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_CostSharingRules_Tenant_Partner_Active",
                table: "CostSharingRules",
                columns: new[] { "TenantId", "DeliveryPartnerId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CostSharingRules_Tenant_Payment_Active",
                table: "CostSharingRules",
                columns: new[] { "TenantId", "PaymentMethodId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CostSharingRules_Tenant_Type_Scope_Active",
                table: "CostSharingRules",
                columns: new[] { "TenantId", "ProviderType", "Scope", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderProfitabilitySnapshotItems_SnapshotId",
                table: "OrderProfitabilitySnapshotItems",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderProfitabilitySnapshotItems_Tenant_Product",
                table: "OrderProfitabilitySnapshotItems",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderProfitabilitySnapshotItems_Tenant_Snapshot",
                table: "OrderProfitabilitySnapshotItems",
                columns: new[] { "TenantId", "SnapshotId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderProfitabilitySnapshots_Order",
                table: "OrderProfitabilitySnapshots",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderProfitabilitySnapshots_Tenant_Cashier_PaidAt",
                table: "OrderProfitabilitySnapshots",
                columns: new[] { "TenantId", "CashierId", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderProfitabilitySnapshots_Tenant_PaidAt",
                table: "OrderProfitabilitySnapshots",
                columns: new[] { "TenantId", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderProfitabilitySnapshots_Tenant_Partner_PaidAt",
                table: "OrderProfitabilitySnapshots",
                columns: new[] { "TenantId", "DeliveryPartnerId", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderProfitabilitySnapshots_Tenant_Payment_PaidAt",
                table: "OrderProfitabilitySnapshots",
                columns: new[] { "TenantId", "PaymentMethodId", "PaidAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CostSharingOverrideAudits");

            migrationBuilder.DropTable(
                name: "CostSharingRules");

            migrationBuilder.DropTable(
                name: "OrderProfitabilitySnapshotItems");

            migrationBuilder.DropTable(
                name: "CostSharingProviders");

            migrationBuilder.DropTable(
                name: "OrderProfitabilitySnapshots");

            migrationBuilder.DropIndex(
                name: "IX_WasteLogs_TenantId_WasteDate",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "WasteDate",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "ChangedFields",
                table: "WasteLogAudits");

            migrationBuilder.DropColumn(
                name: "NewValues",
                table: "WasteLogAudits");

            migrationBuilder.DropColumn(
                name: "PreviousValues",
                table: "WasteLogAudits");

            migrationBuilder.DropColumn(
                name: "CostSharingCommissionAmount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CostSharingCommissionPercentage",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CostSharingCounterpartyPercentage",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CostSharingCounterpartyShareAmount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CostSharingMode",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CostSharingRestaurantPercentage",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CostSharingRestaurantShareAmount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CostSharingScope",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CostSharingCommissionPercentage",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "CostSharingCounterpartyPercentage",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "CostSharingMode",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "CostSharingRestaurantPercentage",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "CostSharingScope",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "CostSharingCalculatedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CostSharingCounterpartyShare",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CostSharingDetailsJson",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CostSharingNetSettlement",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CostSharingRestaurantShare",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CostSharingTotalCommission",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CostSharingCommissionPercentage",
                table: "DeliveryPartners");

            migrationBuilder.DropColumn(
                name: "CostSharingCounterpartyPercentage",
                table: "DeliveryPartners");

            migrationBuilder.DropColumn(
                name: "CostSharingMode",
                table: "DeliveryPartners");

            migrationBuilder.DropColumn(
                name: "CostSharingRestaurantPercentage",
                table: "DeliveryPartners");

            migrationBuilder.DropColumn(
                name: "CostSharingScope",
                table: "DeliveryPartners");
        }
    }
}
