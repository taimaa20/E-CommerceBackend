using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Services;

using RestaurantPos.Api.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;
using System.IO.Compression;
using System.Text;
using System.Threading.RateLimiting;
using RestaurantPos.Api.Interfaces;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Modules.Marketing;
using RestaurantPos.Api.Modules.Payments;
using RestaurantPos.Api.Modules.Purchasing;
using RestaurantPos.Api.Modules.Retail;
using RestaurantPos.Api.Modules.Retail.Import;
using RestaurantPos.Api.Modules.Retail.Services;
using RestaurantPos.Api.Services.Categroy;
using RestaurantPos.Api.Repositories.Category;
using RestaurantPos.Api.Options;

var builder = WebApplication.CreateBuilder(args);
const string DatabaseStartupTasksKey = "Database:RunStartupTasks";
const string BackgroundJobsEnabledKey = "BackgroundJobs:Enabled";
const string DashboardIndexesEnabledKey = $"{DashboardOptions.SectionName}:EnsureIndexesOnStartup";
const string PrintQueueProcessorEnabledKey = "Printing:QueueProcessorEnabled";
const string RetailWorkbookImportSwitch = "--import-retail-workbook";
const string RetailStockLedgerInitSwitch = "--init-retail-stock-ledger";
const string DryRunSwitch = "--dry-run";

var runDatabaseStartupTasks = IsLocalSafeStartupTaskEnabled(builder, DatabaseStartupTasksKey);
var runBackgroundJobs = IsLocalSafeStartupTaskEnabled(builder, BackgroundJobsEnabledKey);
var runDashboardIndexes = IsLocalSafeStartupTaskEnabled(builder, DashboardIndexesEnabledKey);
var runPrintQueueProcessor = IsLocalSafeStartupTaskEnabled(builder, PrintQueueProcessorEnabledKey);

// Lets the same binary also run as a Windows service when launched by
// sc.exe (used by the in-store print-agent install). When the host wasn't
// started by the Service Control Manager — which is always the case on
// Azure App Service — this call is a no-op and Kestrel/IIS hosting
// continues exactly as before. Zero behavioral impact on the cloud deploy.
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "POSPrintAgent";
});

// Add services to the container.
builder.Services.AddDbContext<PosDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// SaaS Multi-Tenancy
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantResolver, HeaderTenantResolver>();
builder.Services.AddScoped<RestaurantPos.Api.Security.ICurrentUserAccessor, RestaurantPos.Api.Security.CurrentUserAccessor>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<ICurrentBranchProvider, HeaderCurrentBranchProvider>();
builder.Services.AddScoped<IBranchResolver, BranchResolver>();
builder.Services.AddScoped<IBranchContext, BranchContext>();

builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IOrderMetricsService, OrderMetricsService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IStockAdjustmentService, StockAdjustmentService>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();
builder.Services.AddScoped<IProcurementService, ProcurementService>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IProcurementRepository,
                            RestaurantPos.Api.Repositories.ProcurementRepository>();
builder.Services.AddScoped<IStockReceiptApprovalService, StockReceiptApprovalService>();
builder.Services.AddScoped<IPurchaseOrderWorkflowService, PurchaseOrderWorkflowService>();
builder.Services.AddScoped<ISimplePurchaseService, SimplePurchaseService>();
// Image variant pipeline (thumb/medium/full WebP). Singleton — stateless and
// purely CPU-bound, safe to share across requests. Consumed by UploadsController.
builder.Services.AddSingleton<IImageProcessingService, ImageProcessingService>();
builder.Services.AddScoped<ITimeTrackingService, TimeTrackingService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IOnlineShoppingService, OnlineShoppingService>();
builder.Services.AddScoped<IOnlineStoreCatalogService, OnlineStoreCatalogService>();
builder.Services.AddScoped<IOnlineShoppingCheckoutService, OnlineShoppingCheckoutService>();
builder.Services.AddScoped<IStorefrontOfferService, StorefrontOfferService>();
builder.Services.AddScoped<IStorefrontBannerService, StorefrontBannerService>();
builder.Services.AddScoped<IProductBrandService, ProductBrandService>();
builder.Services.AddScoped<ICustomerManagementService, CustomerManagementService>();
builder.Services.AddScoped<IDiscountGroupService, DiscountGroupService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<ICurrentUserResolver, CurrentUserResolver>();
builder.Services.AddMarketingEngine();
builder.Services.AddPaymentEngineInfrastructure();
builder.Services.AddRetailModule();
builder.Services.AddPurchasingModule();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Services.ICustomerMobileContext,
                            RestaurantPos.Api.Modules.CustomerMobile.Services.CustomerMobileContext>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Services.ICustomerOtpProvider,
                            RestaurantPos.Api.Modules.CustomerMobile.Services.NoOpCustomerOtpProvider>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Services.ICustomerAuthService,
                            RestaurantPos.Api.Modules.CustomerMobile.Services.CustomerAuthService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Services.ICustomerPhoneAuthService,
                            RestaurantPos.Api.Modules.CustomerMobile.Services.CustomerPhoneAuthService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Services.ICustomerProfileService,
                            RestaurantPos.Api.Modules.CustomerMobile.Services.CustomerProfileService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Services.ICustomerCartService,
                            RestaurantPos.Api.Modules.CustomerMobile.Services.CustomerCartService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Services.ICustomerOrderService,
                            RestaurantPos.Api.Modules.CustomerMobile.Services.CustomerOrderService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Services.ICustomerHomeService,
                            RestaurantPos.Api.Modules.CustomerMobile.Services.CustomerHomeService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Services.ICustomerDeliveryService,
                            RestaurantPos.Api.Modules.CustomerMobile.Services.CustomerDeliveryService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Services.ICustomerCheckoutService,
                            RestaurantPos.Api.Modules.CustomerMobile.Services.CustomerCheckoutService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Repositories.ICustomerMobileBranchRepository,
                            RestaurantPos.Api.Modules.CustomerMobile.Repositories.CustomerMobileBranchRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Repositories.ICustomerCheckoutRepository,
                            RestaurantPos.Api.Modules.CustomerMobile.Repositories.CustomerCheckoutRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Services.ICustomerMobileBranchService,
                            RestaurantPos.Api.Modules.CustomerMobile.Services.CustomerMobileBranchService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Services.ICustomerOrderTrackingService,
                            RestaurantPos.Api.Modules.CustomerMobile.Services.CustomerOrderTrackingService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Services.ICustomerReorderService,
                            RestaurantPos.Api.Modules.CustomerMobile.Services.CustomerReorderService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Services.ICustomerDeviceService,
                            RestaurantPos.Api.Modules.CustomerMobile.Services.CustomerDeviceService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Services.ICustomerOrderCancellationService,
                            RestaurantPos.Api.Modules.CustomerMobile.Services.CustomerOrderCancellationService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Services.ICustomerMobileSettingsService,
                            RestaurantPos.Api.Modules.CustomerMobile.Services.CustomerMobileSettingsService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Services.ICustomerLoyaltyService,
                            RestaurantPos.Api.Modules.CustomerMobile.Services.CustomerLoyaltyService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.CustomerMobile.Services.ICustomerNotificationService,
                            RestaurantPos.Api.Modules.CustomerMobile.Services.CustomerNotificationService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ICashierShiftService, CashierShiftService>();
builder.Services.AddScoped<ICancelLogService, CancelLogService>();
builder.Services.AddScoped<ICancelReasonService, CancelReasonService>();
builder.Services.AddScoped<IExpiryJobService, ExpiryJobService>();
builder.Services.AddScoped<IVoucherService, VoucherService>();
builder.Services.AddSingleton<IBackgroundJob, ExpiryBackgroundJob>();
builder.Services.AddScoped<ICategoryServices, CategoryServices>();
// Additional maintenance / alert jobs — see each BackgroundJob class for cadence.
builder.Services.AddScoped<IRefreshTokenCleanupJobService, RefreshTokenCleanupJobService>();
builder.Services.AddSingleton<IBackgroundJob, RefreshTokenCleanupBackgroundJob>();

builder.Services.AddScoped<IStaleCashierShiftAlertJobService, StaleCashierShiftAlertJobService>();
builder.Services.AddSingleton<IBackgroundJob, StaleCashierShiftAlertBackgroundJob>();

builder.Services.AddScoped<ILowStockAlertJobService, LowStockAlertJobService>();
builder.Services.AddSingleton<IBackgroundJob, LowStockAlertBackgroundJob>();

builder.Services.AddScoped<INotificationCleanupJobService, NotificationCleanupJobService>();
builder.Services.AddSingleton<IBackgroundJob, NotificationCleanupBackgroundJob>();

builder.Services.AddScoped<IIdempotencyCleanupJobService, IdempotencyCleanupJobService>();
builder.Services.AddSingleton<IBackgroundJob, IdempotencyCleanupBackgroundJob>();

// Loyalty (Marketing) Phase 2 automation jobs — scoped services live in AddMarketingEngine().
builder.Services.AddSingleton<IBackgroundJob, RestaurantPos.Api.Modules.Marketing.Jobs.LoyaltyExpirationBackgroundJob>();
builder.Services.AddSingleton<IBackgroundJob, RestaurantPos.Api.Modules.Marketing.Jobs.TierRecalculationBackgroundJob>();
builder.Services.AddSingleton<IBackgroundJob, RestaurantPos.Api.Modules.Marketing.Jobs.SegmentComputationBackgroundJob>();
builder.Services.AddSingleton<IBackgroundJob, RestaurantPos.Api.Modules.Marketing.Jobs.PendingApprovalBackgroundJob>();
builder.Services.AddSingleton<IBackgroundJob, RestaurantPos.Api.Modules.Marketing.Jobs.CampaignProcessingBackgroundJob>();

// Register MediatR (scans the current assembly for Handlers)
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Add MemoryCache for rate limiting
builder.Services.AddMemoryCache();

// === CACHING INFRASTRUCTURE ===
builder.Services.AddSingleton<RestaurantPos.Api.Services.Caching.ICacheService, RestaurantPos.Api.Services.Caching.MemoryCacheService>();

builder.Services.Configure<RestaurantPos.Api.Options.DeliveryPartnersOptions>(
    builder.Configuration.GetSection(RestaurantPos.Api.Options.DeliveryPartnersOptions.SectionName));
builder.Services.Configure<DashboardOptions>(
    builder.Configuration.GetSection(DashboardOptions.SectionName));
builder.Services.AddScoped<RestaurantPos.Api.Filters.DeliveryPartnersFeatureFilter>();

// === REPOSITORIES (Data Access Layer) ===
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IProductRepository, RestaurantPos.Api.Repositories.ProductRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IOnlineShoppingRepository, RestaurantPos.Api.Repositories.OnlineShoppingRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IStorefrontCatalogRepository, RestaurantPos.Api.Repositories.StorefrontCatalogRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IStorefrontBannerRepository, RestaurantPos.Api.Repositories.StorefrontBannerRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IProductBrandRepository, RestaurantPos.Api.Repositories.ProductBrandRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.ISettingsRepository, RestaurantPos.Api.Repositories.SettingsRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IFollowUsClickRepository, RestaurantPos.Api.Repositories.FollowUsClickRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IDeliveryPartnerRepository, RestaurantPos.Api.Repositories.DeliveryPartnerRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IPaymentRepository, RestaurantPos.Api.Repositories.PaymentRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IPaymentMethodRepository, RestaurantPos.Api.Repositories.PaymentMethodRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IPaymentAdjustmentRepository, RestaurantPos.Api.Repositories.PaymentAdjustmentRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.ICostProfitabilityRepository, RestaurantPos.Api.Repositories.CostProfitabilityRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IVoucherRepository, RestaurantPos.Api.Repositories.VoucherRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IOrderRepository, RestaurantPos.Api.Repositories.OrderRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.ICustomerManagementRepository, RestaurantPos.Api.Repositories.CustomerManagementRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IPublicReceiptRepository, RestaurantPos.Api.Repositories.PublicReceiptRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.ICashierShiftRepository, RestaurantPos.Api.Repositories.CashierShiftRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IMobileHrRequestRepository, RestaurantPos.Api.Repositories.MobileHrRequestRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IBonusRepository, RestaurantPos.Api.Repositories.BonusRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IHrEmployeeRepository, RestaurantPos.Api.Repositories.HrEmployeeRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IPosLogRepository, RestaurantPos.Api.Repositories.PosLogRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IWasteLogRepository, RestaurantPos.Api.Repositories.WasteLogRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IRawMaterialConsumptionRepository, RestaurantPos.Api.Repositories.RawMaterialConsumptionRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IBranchRepository, RestaurantPos.Api.Repositories.BranchRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IUserBranchRepository, RestaurantPos.Api.Repositories.UserBranchRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IBranchConfigurationRepository, RestaurantPos.Api.Repositories.BranchConfigurationRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.ITableCategoryRepository, RestaurantPos.Api.Repositories.TableCategoryRepository>();

// === SERVICES (Business Logic with Caching) ===
builder.Services.AddScoped<RestaurantPos.Api.Services.IProductService, RestaurantPos.Api.Services.ProductService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.ISubcategoryService, RestaurantPos.Api.Services.SubcategoryService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.Pricing.IPricingEngine, RestaurantPos.Api.Services.Pricing.PricingEngine>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IDeliveryPartnerService, RestaurantPos.Api.Services.DeliveryPartnerService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IPaymentMethodService, RestaurantPos.Api.Services.PaymentMethodService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IPaymentAdjustmentService, RestaurantPos.Api.Services.PaymentAdjustmentService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.ICostProfitabilityService, RestaurantPos.Api.Services.CostProfitabilityService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.ITableCategoryService, RestaurantPos.Api.Services.TableCategoryService>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IDeliveryZoneRepository, RestaurantPos.Api.Repositories.DeliveryZoneRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IDeliveryZoneService, RestaurantPos.Api.Services.DeliveryZoneService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IOrderService, RestaurantPos.Api.Services.OrderService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IOrderDisplayNumberService, RestaurantPos.Api.Services.OrderDisplayNumberService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IOrderNumberingConfigService, RestaurantPos.Api.Services.OrderNumberingConfigService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IShiftRulesConfigService, RestaurantPos.Api.Services.ShiftRulesConfigService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IShiftCloseValidator, RestaurantPos.Api.Services.ShiftCloseValidator>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IShiftSummaryService, RestaurantPos.Api.Services.ShiftSummaryService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IConfigAuditService, RestaurantPos.Api.Services.ConfigAuditService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IPublicReceiptService, RestaurantPos.Api.Services.PublicReceiptService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.ISettingsService, RestaurantPos.Api.Services.SettingsService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IFollowUsClickService, RestaurantPos.Api.Services.FollowUsClickService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IMobileLeaveRequestService, RestaurantPos.Api.Services.MobileLeaveRequestService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IMobileLoanRequestService, RestaurantPos.Api.Services.MobileLoanRequestService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IMobilePermissionRequestService, RestaurantPos.Api.Services.MobilePermissionRequestService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IBonusService, RestaurantPos.Api.Services.BonusService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IHrEmployeeService, RestaurantPos.Api.Services.HrEmployeeService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IStaffService, RestaurantPos.Api.Services.StaffService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IPosLogService, RestaurantPos.Api.Services.PosLogService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IWasteLogService, RestaurantPos.Api.Services.WasteLogService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IRawMaterialConsumptionService, RestaurantPos.Api.Services.RawMaterialConsumptionService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IRawMaterialConsumptionExportService, RestaurantPos.Api.Services.RawMaterialConsumptionExportService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IBranchService, RestaurantPos.Api.Services.BranchService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IUserBranchService, RestaurantPos.Api.Services.UserBranchService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IBranchConfigurationService, RestaurantPos.Api.Services.BranchConfigurationService>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.ICustomerAnalyticsRepository,
                            RestaurantPos.Api.Repositories.CustomerAnalyticsRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Services.ICustomerAnalyticsService,
                            RestaurantPos.Api.Services.CustomerAnalyticsService>();

// ─── Expense Invoices module ───────────────────────────────────────────────
// File storage is provider-agnostic — swap LocalFileStorageService for an
// Azure Blob / S3 implementation later without touching the repo / service /
// controller. Scoped because the local impl reads IHttpContextAccessor per request.
builder.Services.AddScoped<RestaurantPos.Api.Services.Storage.IFileStorageService,
                            RestaurantPos.Api.Services.Storage.LocalFileStorageService>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IExpenseInvoiceRepository,
                            RestaurantPos.Api.Repositories.ExpenseInvoiceRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IExpenseInvoiceService,
                            RestaurantPos.Api.Services.ExpenseInvoiceService>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IExpenseCategoryRepository,
                            RestaurantPos.Api.Repositories.ExpenseCategoryRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IExpenseCategoryService,
                            RestaurantPos.Api.Services.ExpenseCategoryService>();

// Cashier shift expenses — shift-scoped slice of the same ExpenseInvoice table.
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IShiftExpenseRepository,
                            RestaurantPos.Api.Repositories.ShiftExpenseRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IShiftExpenseService,
                            RestaurantPos.Api.Services.ShiftExpenseService>();

// ─── Assets Management module ─────────────────────────────────────────────
builder.Services.Configure<RestaurantPos.Api.Services.Assets.AssetStorageOptions>(
    builder.Configuration.GetSection("AssetStorage"));
builder.Services.AddScoped<RestaurantPos.Api.Services.Assets.IAssetFileStorageService,
                            RestaurantPos.Api.Services.Assets.AssetFileStorageService>();
builder.Services.AddScoped<RestaurantPos.Api.Repositories.IAssetRepository,
                            RestaurantPos.Api.Repositories.AssetRepository>();
builder.Services.AddScoped<RestaurantPos.Api.Services.IAssetService,
                            RestaurantPos.Api.Services.AssetService>();

// Response compression — Brotli + Gzip over HTTPS.
// The public menu endpoints (/products/public, /categories/public, /offers/public)
// return deeply-nested JSON (modifier groups, recipe items, alternatives). Uncompressed
// payloads for a typical 100-product catalogue sit in the 1–5 MB range; Brotli brings
// that to ~150–500 KB on the wire, which is the single biggest win for /menu load time.
// Safe for public menu JSON: no user-controlled input is echoed into the payload, so
// BREACH-style side channels don't apply.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json",
        "application/json; charset=utf-8"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    // Fastest: still compresses nested JSON ~5–10× while keeping server CPU negligible.
    options.Level = CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.PermitLimit = 60;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    options.RejectionStatusCode = 429;
});


builder.Services.AddControllers(options =>
{
    // Global dashboard-filter binder. The filter is a no-op for any action
    // whose controller is not decorated with [DashboardScope], so it's safe
    // to apply universally.
    options.Filters.AddService<RestaurantPos.Api.Modules.Dashboard.Filters.DashboardFilterBindingFilter>();
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var path = context.HttpContext.Request.Path;
        if (path.StartsWithSegments("/api/Orders", StringComparison.OrdinalIgnoreCase))
        {
            var errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors
                        .Select(error => error.ErrorMessage)
                        .ToArray());
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("OrderModelValidation");
            logger.LogWarning(
                "Order request ModelState validation failed on {Path}: {@ValidationErrors}",
                path,
                errors);
        }

        var problemDetailsFactory = context.HttpContext.RequestServices
            .GetRequiredService<Microsoft.AspNetCore.Mvc.Infrastructure.ProblemDetailsFactory>();
        var problemDetails = problemDetailsFactory.CreateValidationProblemDetails(
            context.HttpContext,
            context.ModelState,
            statusCode: StatusCodes.Status400BadRequest);
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(problemDetails);
    };
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    var secret = builder.Configuration["Jwt:Secret"] ?? builder.Configuration["Jwt:Key"];
    if (string.IsNullOrWhiteSpace(secret))
    {
        return;
    }

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
        ClockSkew = TimeSpan.Zero
    };
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Restaurant POS API", Version = "v1" });
    c.CustomSchemaIds(type => type.FullName?.Replace('+', '.') ?? type.Name);

    // Swagger'a Kilit Butonunu (JWT) Ekleme
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Token'ınızı şu formatta giriniz: Bearer [boşluk] [token]",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

builder.Services.AddSignalR();
builder.Services.AddSingleton<RestaurantPos.Api.Services.INotificationService, RestaurantPos.Api.Services.NotificationService>();
if (runBackgroundJobs)
{
    builder.Services.AddHostedService<BackgroundJobHostedService>();
}

// ─── Dashboard module ───────────────────────────────────────────────────────
// Filter engine (scoped — one envelope per request)
builder.Services.AddScoped<RestaurantPos.Api.Modules.Dashboard.Filters.IDashboardFilterContext,
                           RestaurantPos.Api.Modules.Dashboard.Filters.DashboardFilterContext>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.Dashboard.Filters.DashboardFilterBindingFilter>();

// Query builder + per-scope analytics services
builder.Services.AddScoped<RestaurantPos.Api.Modules.Dashboard.Queries.IMetricQueryBuilder,
                           RestaurantPos.Api.Modules.Dashboard.Queries.MetricQueryBuilder>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.Dashboard.Services.Operational.IOperationalMetricsService,
                           RestaurantPos.Api.Modules.Dashboard.Services.Operational.OperationalMetricsService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.Dashboard.Services.Financial.IFinancialAnalyticsService,
                           RestaurantPos.Api.Modules.Dashboard.Services.Financial.FinancialAnalyticsService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.Dashboard.Services.Financial.IFinancialDashboardExportService,
                           RestaurantPos.Api.Modules.Dashboard.Services.Financial.FinancialDashboardExportService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.Dashboard.Services.Inventory.IInventoryAnalyticsService,
                           RestaurantPos.Api.Modules.Dashboard.Services.Inventory.InventoryAnalyticsService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.Dashboard.Services.Audit.IAuditAnalyticsService,
                           RestaurantPos.Api.Modules.Dashboard.Services.Audit.AuditAnalyticsService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.Dashboard.Services.Home.IHomeAnalyticsService,
                           RestaurantPos.Api.Modules.Dashboard.Services.Home.HomeAnalyticsService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.Dashboard.Services.Home.IHomeDashboardExportService,
                           RestaurantPos.Api.Modules.Dashboard.Services.Home.HomeDashboardExportService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.Dashboard.Services.Operational.IOperationsDashboardExportService,
                           RestaurantPos.Api.Modules.Dashboard.Services.Operational.OperationsDashboardExportService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.Dashboard.Services.Inventory.IInventoryDashboardExportService,
                           RestaurantPos.Api.Modules.Dashboard.Services.Inventory.InventoryDashboardExportService>();
builder.Services.AddScoped<RestaurantPos.Api.Modules.Dashboard.Services.IDashboardMetricsService,
                           RestaurantPos.Api.Modules.Dashboard.Services.DashboardMetricsService>();

// Caching + realtime layer
// Cache wrapper is scoped because it pulls the tenant id from the scoped
// ITenantResolver. The underlying ICacheService is singleton (shared across
// requests). Realtime is singleton — debouncer state is shared by design.
builder.Services.AddScoped<RestaurantPos.Api.Modules.Dashboard.Services.Caching.IDashboardCacheService,
                           RestaurantPos.Api.Modules.Dashboard.Services.Caching.DashboardCacheService>();
builder.Services.AddSingleton<RestaurantPos.Api.Modules.Dashboard.Services.Realtime.IDashboardRealtimeService,
                              RestaurantPos.Api.Modules.Dashboard.Services.Realtime.DashboardRealtimeService>();

// Performance index ensurer — runs once on startup, idempotent.
if (runDashboardIndexes)
{
    builder.Services.AddHostedService<RestaurantPos.Api.Modules.Dashboard.Infrastructure.DashboardIndexInitializer>();
}

// ─── Multi-kitchen routing & direct printing ────────────────────────────────
// Routing/builder/queue/dispatcher are scoped (per-request DbContext).
// NetworkPrinterClient is stateless → singleton.
// PrintQueueProcessorBackgroundService is a hosted singleton with its own scope per tick.
builder.Services.AddScoped<RestaurantPos.Api.Services.Printing.IKitchenRoutingService, RestaurantPos.Api.Services.Printing.KitchenRoutingService>();
builder.Services.AddSingleton<RestaurantPos.Api.Services.Printing.IKitchenTicketBuilder, RestaurantPos.Api.Services.Printing.KitchenTicketBuilder>();
builder.Services.AddScoped<RestaurantPos.Api.Services.Printing.IPrintQueueService, RestaurantPos.Api.Services.Printing.PrintQueueService>();
builder.Services.AddScoped<RestaurantPos.Api.Services.Printing.IPrintDispatcher, RestaurantPos.Api.Services.Printing.PrintDispatcher>();
builder.Services.AddSingleton<RestaurantPos.Api.Services.Printing.INetworkPrinterClient, RestaurantPos.Api.Services.Printing.NetworkPrinterClient>();
if (runPrintQueueProcessor)
{
    builder.Services.AddHostedService<RestaurantPos.Api.Services.Printing.PrintQueueProcessorBackgroundService>();
}

// ─── Local Print Agent (in-store bridge for cloud → LAN/USB printers) ──────
// Activates ONLY when the appsettings/env var "PrintAgent:Enabled = true",
// which is never the case on the Azure App Service deployment. On Azure
// none of these services are registered — zero CPU, zero memory, zero
// behavior change vs. the pre-agent codebase.
//
// On a Windows PC inside the store running the same publish artifact with
// PrintAgent__Enabled=true, the hosted service polls the cloud, claims
// jobs whose printer has UseLocalAgent=true, and prints them locally
// (TCP for network printers, Windows spooler RAW for USB printers).
// Activate the in-store print agent only when:
//   1. The flag is on (false on Azure, true only on store PC), AND
//   2. We are running on Windows. The Windows guard satisfies CA1416 for
//      AgentWindowsSpoolerWriter (Win32 PInvoke) and is the only supported
//      target for the store-side deployment anyway. On non-Windows hosts
//      the agent is silently inert even with the flag on.
var printAgentEnabled = builder.Configuration.GetValue<bool>("PrintAgent:Enabled");
if (printAgentEnabled && OperatingSystem.IsWindows())
{
    builder.Services.Configure<RestaurantPos.Api.Services.Printing.Agent.PrintAgentSettings>(
        builder.Configuration.GetSection("PrintAgent"));
    builder.Services.AddHttpClient<RestaurantPos.Api.Services.Printing.Agent.AgentApiClient>();
    builder.Services.AddSingleton<RestaurantPos.Api.Services.Printing.Agent.Writers.AgentNetworkWriter>();
    builder.Services.AddSingleton<RestaurantPos.Api.Services.Printing.Agent.Writers.AgentWindowsSpoolerWriter>();
    builder.Services.AddSingleton<RestaurantPos.Api.Services.Printing.Agent.AgentHtmlWriter>();
    builder.Services.AddHostedService<RestaurantPos.Api.Services.Printing.Agent.LocalPrintAgentBackgroundService>();
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNextJs",
        builder => builder
        .SetIsOriginAllowed(_ => true) // Allow any origin for Dev (localhost, network IP)
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()); // Required for SignalR
});


var app = builder.Build();

if (runDatabaseStartupTasks)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PosDbContext>();
    await dbContext.Database.MigrateAsync();
    await RestaurantPos.Api.Data.WarehouseBackfill.RunAsync(dbContext);
}
else
{
    app.Logger.LogInformation("Database startup tasks are disabled by {ConfigKey}.", DatabaseStartupTasksKey);
}

// Developer-side sync of the DOHA LUXE retail workbook. Runs the sync and exits without
// ever serving traffic:
//   dotnet run -- --import-retail-workbook [path-to.xlsx] [--dry-run]
// Incremental and idempotent — safe to re-run against a database that already holds an
// earlier revision of the workbook plus live trading on top of it. --dry-run reports the
// full plan and writes nothing at all.
if (args.Contains(RetailWorkbookImportSwitch))
{
    using var importScope = app.Services.CreateScope();
    var importer = importScope.ServiceProvider.GetRequiredService<IRetailWorkbookImporter>();
    var workbookPath = ResolveRetailWorkbookPath(args);
    var isDryRun = args.Contains(DryRunSwitch);

    var importResult = await importer.RunAsync(workbookPath, isDryRun);
    Console.WriteLine(importResult.ToReport());

    if (isDryRun)
        return;

    // The sync leaves the stock ledger consistent with what it just wrote, so the two can
    // never drift apart. Both steps are idempotent, so re-running converges on the same state.
    Console.WriteLine(RenderLedgerReport(
        await importScope.ServiceProvider
            .GetRequiredService<IRetailStockLedgerInitializer>()
            .InitializeAsync()));
    return;
}

// Converts the imported opening position into stock-ledger movements and reconciles every
// historical retail order onto it:
//   dotnet run -- --init-retail-stock-ledger
// Idempotent — the opening balance is unique per product and branch, each sale unique per line.
if (args.Contains(RetailStockLedgerInitSwitch))
{
    using var ledgerScope = app.Services.CreateScope();
    var initializer = ledgerScope.ServiceProvider.GetRequiredService<IRetailStockLedgerInitializer>();
    Console.WriteLine(RenderLedgerReport(await initializer.InitializeAsync()));
    return;
}

static string RenderLedgerReport(RetailStockLedgerInitResult result)
{
    var report = new System.Text.StringBuilder();
    report.AppendLine("Retail stock ledger");
    report.AppendLine("-------------------");
    report.AppendLine($"  Products with an opening balance : {result.ProductsWithOpeningBalance}");
    report.AppendLine($"  Opening movements created       : {result.OpeningBalancesCreated}");
    report.AppendLine($"  Opening units                   : {result.OpeningUnits:0.##}");
    report.AppendLine($"  Orders reconciled               : {result.OrdersReconciled}");
    report.AppendLine($"  Units sold                      : {result.SoldUnits:0.##}");
    report.AppendLine($"  Stock on hand                   : {result.StockOnHand:0.##}");

    foreach (var skipped in result.Skipped)
        report.AppendLine($"  SKIPPED: {skipped}");

    return report.ToString();
}

// Configure the HTTP request pipeline.
// Typed-exception → HTTP-status mapping. Must be registered FIRST so it wraps every
// downstream middleware and controller. See Middleware/ExceptionMiddleware.cs.
app.UseMiddleware<RestaurantPos.Api.Middleware.ExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// Must run before CORS/Auth so response body is compressed regardless of origin.
app.UseResponseCompression();

// Private Network Access for /local/* endpoints. Chrome's PNA blocks an HTTPS
// POS page from calling http://127.0.0.1 unless the loopback server returns
// `Access-Control-Allow-Private-Network: true` on the preflight AND the actual
// response. This must run BEFORE UseCors — the built-in CORS middleware
// short-circuits OPTIONS preflights before the next middleware runs, so we
// fully handle /local/* preflights here ourselves.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (path == null || !path.StartsWith("/local/", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    var origin = context.Request.Headers["Origin"].ToString();
    context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
    context.Response.Headers["Access-Control-Allow-Origin"] = string.IsNullOrEmpty(origin) ? "*" : origin;
    context.Response.Headers["Access-Control-Allow-Headers"] = "content-type";
    context.Response.Headers["Access-Control-Allow-Methods"] = "POST, OPTIONS";
    context.Response.Headers["Vary"] = "Origin";

    if (HttpMethods.IsOptions(context.Request.Method))
    {
        // Short-circuit before CORS middleware so our PNA headers stick.
        context.Response.StatusCode = StatusCodes.Status204NoContent;
        return;
    }

    await next();
});

app.UseCors("AllowNextJs");

app.UseRateLimiter();

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    await next();
});

// Serve uploaded images from wwwroot/uploads.
// Long max-age + immutable: uploaded file names are content-unique (timestamp + GUID),
// so a given URL can never serve different bytes — safe to cache aggressively.
app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value;
        if (!string.IsNullOrEmpty(path) &&
            path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
        }
    }
});

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true && context.User.IsInRole(AppRoleNames.TrackerPickup))
    {
        if (!TrackerPickupAccessPolicy.IsAllowed(context.Request.Path, context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("TrackerPickup can only access the display tracker actions.");
            return;
        }
    }

    await next();
});

// SaaS Tenant Resolution Middleware
app.UseMiddleware<RestaurantPos.Api.Middleware.TenantMiddleware>();

// Idempotency — must run after tenant resolution so cache keys are scoped correctly
// if we later include tenant in the key, and before controllers so replayed responses
// bypass the action pipeline entirely.
app.UseMiddleware<RestaurantPos.Api.Middleware.IdempotencyMiddleware>();

app.MapControllers();
app.MapHub<KitchenHub>("/kitchenHub");
app.MapHub<RestaurantPos.Api.Modules.CustomerMobile.Hubs.MobileOrderHub>("/mobile-order-hub");
app.MapHub<RestaurantPos.Api.Modules.Dashboard.Hubs.DashboardHub>("/dashboardHub");

app.Run();

static bool IsLocalSafeStartupTaskEnabled(WebApplicationBuilder builder, string configKey)
{
    var configured = builder.Configuration.GetValue<bool?>(configKey);
    return configured ?? !builder.Environment.IsDevelopment();
}

// Path argument that follows --import-retail-workbook, falling back to the workbook
// vendored in the repository so the switch works with no further arguments.
static string ResolveRetailWorkbookPath(string[] args)
{
    var switchIndex = Array.IndexOf(args, "--import-retail-workbook");
    var next = switchIndex >= 0 && switchIndex + 1 < args.Length ? args[switchIndex + 1] : null;

    return next is not null && !next.StartsWith("--", StringComparison.Ordinal)
        ? next
        : "../reference/DOHA_LUXE_Business_Management_System.xlsx";
}
