using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketingEngineFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LoyaltyCustomerCode",
                table: "Customers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerMergeHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SurvivorCustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    MergedCustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    MergedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MergedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PointsTransferred = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    WalletTransactionsRekeyed = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerMergeHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerMergeHistories_Customers_MergedCustomerId",
                        column: x => x.MergedCustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerMergeHistories_Customers_SurvivorCustomerId",
                        column: x => x.SurvivorCustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastComputedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerSegments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EarningRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleVersion = table.Column<int>(type: "integer", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    RuleType = table.Column<int>(type: "integer", nullable: false),
                    PointsValue = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PointsPerCurrencyUnit = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Multiplier = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    TargetCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChannelScope = table.Column<int>(type: "integer", nullable: true),
                    ApprovalMode = table.Column<int>(type: "integer", nullable: false),
                    ApprovalDelayDays = table.Column<int>(type: "integer", nullable: true),
                    ExpirationMode = table.Column<int>(type: "integer", nullable: false),
                    ExpirationValue = table.Column<int>(type: "integer", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EarningRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EarningRules_Categories_TargetCategoryId",
                        column: x => x.TargetCategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EarningRules_Products_TargetProductId",
                        column: x => x.TargetProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LegacyTierEnum = table.Column<int>(type: "integer", nullable: false),
                    MinPoints = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MinSpend = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EarnMultiplier = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyTiers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketingAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    BeforeJson = table.Column<string>(type: "jsonb", nullable: true),
                    AfterJson = table.Column<string>(type: "jsonb", nullable: true),
                    Metadata = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketingSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    LoyaltyEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RewardsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PromosEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    VouchersEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ReferralsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    InfluencersEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CampaignsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultPointValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DefaultExpirationMode = table.Column<int>(type: "integer", nullable: false),
                    DefaultExpirationValue = table.Column<int>(type: "integer", nullable: true),
                    MinRedeemPoints = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MaxRedeemPoints = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MaxStackedDiscount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    EarnDelegationEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SegmentMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SegmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SegmentMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SegmentMembers_CustomerSegments_SegmentId",
                        column: x => x.SegmentId,
                        principalTable: "CustomerSegments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SegmentMembers_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SegmentRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SegmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Field = table.Column<int>(type: "integer", nullable: false),
                    Operator = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    LogicGroup = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SegmentRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SegmentRules_CustomerSegments_SegmentId",
                        column: x => x.SegmentId,
                        principalTable: "CustomerSegments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerWallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AvailablePoints = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PendingPoints = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ExpiredPoints = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RedeemedPoints = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LifetimePoints = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LifetimeSpend = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TotalOrders = table.Column<int>(type: "integer", nullable: false),
                    TotalVisits = table.Column<int>(type: "integer", nullable: false),
                    CurrentTierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FrozenReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    LastRecalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerWallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerWallets_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerWallets_LoyaltyTiers_CurrentTierId",
                        column: x => x.CurrentTierId,
                        principalTable: "LoyaltyTiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TierBenefits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TierId = table.Column<Guid>(type: "uuid", nullable: false),
                    BenefitType = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    RefId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DescriptionAr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TierBenefits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TierBenefits_LoyaltyTiers_TierId",
                        column: x => x.TierId,
                        principalTable: "LoyaltyTiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WalletTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    RuleVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversesTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    MergeHistoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ReasonAr = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerPhoneSnapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    OrderSourceSnapshot = table.Column<int>(type: "integer", nullable: true),
                    SubtotalSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DiscountSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    TaxSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    TotalAmountSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    EarnedPoints = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletTransactions_CustomerMergeHistories_MergeHistoryId",
                        column: x => x.MergeHistoryId,
                        principalTable: "CustomerMergeHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WalletTransactions_CustomerWallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "CustomerWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WalletTransactions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WalletTransactions_EarningRules_RuleVersionId",
                        column: x => x.RuleVersionId,
                        principalTable: "EarningRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WalletTransactions_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WalletTransactions_WalletTransactions_ReversesTransactionId",
                        column: x => x.ReversesTransactionId,
                        principalTable: "WalletTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PointsLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalPoints = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RemainingPoints = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EarnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointsLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PointsLots_CustomerWallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "CustomerWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PointsLots_WalletTransactions_SourceTransactionId",
                        column: x => x.SourceTransactionId,
                        principalTable: "WalletTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_LoyaltyCustomerCode",
                table: "Customers",
                columns: new[] { "TenantId", "LoyaltyCustomerCode" },
                unique: true,
                filter: "\"LoyaltyCustomerCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMergeHistories_MergedCustomerId",
                table: "CustomerMergeHistories",
                column: "MergedCustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMergeHistories_SurvivorCustomerId",
                table: "CustomerMergeHistories",
                column: "SurvivorCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMergeHistories_TenantId_CreatedAt",
                table: "CustomerMergeHistories",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerSegments_TenantId_BranchId_IsActive",
                table: "CustomerSegments",
                columns: new[] { "TenantId", "BranchId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerSegments_TenantId_Name",
                table: "CustomerSegments",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerWallets_CurrentTierId",
                table: "CustomerWallets",
                column: "CurrentTierId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerWallets_CustomerId",
                table: "CustomerWallets",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerWallets_Status",
                table: "CustomerWallets",
                column: "Status",
                filter: "\"Status\" <> 0");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerWallets_TenantId_BranchId",
                table: "CustomerWallets",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerWallets_TenantId_CustomerId",
                table: "CustomerWallets",
                columns: new[] { "TenantId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EarningRules_CampaignId",
                table: "EarningRules",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_EarningRules_TargetCategoryId",
                table: "EarningRules",
                column: "TargetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EarningRules_TargetProductId",
                table: "EarningRules",
                column: "TargetProductId");

            migrationBuilder.CreateIndex(
                name: "IX_EarningRules_TenantId_BranchId_IsActive_IsCurrent",
                table: "EarningRules",
                columns: new[] { "TenantId", "BranchId", "IsActive", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_EarningRules_TenantId_RuleGroupId",
                table: "EarningRules",
                columns: new[] { "TenantId", "RuleGroupId" },
                unique: true,
                filter: "\"IsCurrent\" = true AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EarningRules_TenantId_RuleGroupId_RuleVersion",
                table: "EarningRules",
                columns: new[] { "TenantId", "RuleGroupId", "RuleVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTiers_TenantId_BranchId_IsActive_MinPoints",
                table: "LoyaltyTiers",
                columns: new[] { "TenantId", "BranchId", "IsActive", "MinPoints" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTiers_TenantId_LegacyTierEnum",
                table: "LoyaltyTiers",
                columns: new[] { "TenantId", "LegacyTierEnum" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTiers_TenantId_SortOrder",
                table: "LoyaltyTiers",
                columns: new[] { "TenantId", "SortOrder" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MarketingAuditLogs_CustomerId_CreatedAt",
                table: "MarketingAuditLogs",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingAuditLogs_TenantId_CreatedAt",
                table: "MarketingAuditLogs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingAuditLogs_TenantId_EntityType_EntityId",
                table: "MarketingAuditLogs",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingSettings_TenantId",
                table: "MarketingSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointsLots_ExpiresAt",
                table: "PointsLots",
                column: "ExpiresAt",
                filter: "\"Status\" = 0 AND \"ExpiresAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PointsLots_SourceTransactionId",
                table: "PointsLots",
                column: "SourceTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointsLots_WalletId_Status_EarnedAt",
                table: "PointsLots",
                columns: new[] { "WalletId", "Status", "EarnedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SegmentMembers_CustomerId",
                table: "SegmentMembers",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SegmentMembers_SegmentId_CustomerId",
                table: "SegmentMembers",
                columns: new[] { "SegmentId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SegmentRules_SegmentId",
                table: "SegmentRules",
                column: "SegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TierBenefits_TierId",
                table: "TierBenefits",
                column: "TierId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_CustomerId_CreatedAt",
                table: "WalletTransactions",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_ExpiresAt",
                table: "WalletTransactions",
                column: "ExpiresAt",
                filter: "\"Status\" = 1 AND \"ExpiresAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_MergeHistoryId",
                table: "WalletTransactions",
                column: "MergeHistoryId",
                filter: "\"MergeHistoryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_OrderId",
                table: "WalletTransactions",
                column: "OrderId",
                filter: "\"OrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_ReversesTransactionId",
                table: "WalletTransactions",
                column: "ReversesTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_RuleVersionId",
                table: "WalletTransactions",
                column: "RuleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_TenantId_CreatedAt",
                table: "WalletTransactions",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_WalletId_IdempotencyKey",
                table: "WalletTransactions",
                columns: new[] { "WalletId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_WalletId_Status",
                table: "WalletTransactions",
                columns: new[] { "WalletId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketingAuditLogs");

            migrationBuilder.DropTable(
                name: "MarketingSettings");

            migrationBuilder.DropTable(
                name: "PointsLots");

            migrationBuilder.DropTable(
                name: "SegmentMembers");

            migrationBuilder.DropTable(
                name: "SegmentRules");

            migrationBuilder.DropTable(
                name: "TierBenefits");

            migrationBuilder.DropTable(
                name: "WalletTransactions");

            migrationBuilder.DropTable(
                name: "CustomerSegments");

            migrationBuilder.DropTable(
                name: "CustomerMergeHistories");

            migrationBuilder.DropTable(
                name: "CustomerWallets");

            migrationBuilder.DropTable(
                name: "EarningRules");

            migrationBuilder.DropTable(
                name: "LoyaltyTiers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_LoyaltyCustomerCode",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LoyaltyCustomerCode",
                table: "Customers");
        }
    }
}
