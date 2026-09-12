using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services.Time;

namespace RestaurantPos.Api.Services
{
    /// <inheritdoc cref="IOrderDisplayNumberService"/>
    public class OrderDisplayNumberService : IOrderDisplayNumberService
    {
        // Per-channel codes — kept here as the single source of truth so the
        // controller, repository search, and any future reporter all agree.
        public const string CodeTakeaway        = "TA";
        public const string CodeDineIn          = "DI";
        public const string CodePartner         = "PT"; // Talabat / Delivery Partner integrations
        public const string CodeDelivery        = "DL"; // In-house delivery (own driver)
        public const string CodeQrMobile        = "QR";
        public const string CodeOnline          = "ON";
        public const string CodeGlobal          = "GLOBAL";
        public const string CodeGlobalDisplay   = "ORD";

        // Legacy code retained as an alias so existing OrderDisplaySequence rows
        // (and any reports filtering on "DP") remain readable. Never written to
        // by new orders — the resolver returns CodePartner / CodeDelivery instead.
        public const string CodeDeliveryPartner = "DP";

        private readonly PosDbContext _context;
        private readonly IOrderNumberingConfigService _configService;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<OrderDisplayNumberService> _logger;

        public OrderDisplayNumberService(
            PosDbContext context,
            IOrderNumberingConfigService configService,
            ISettingsService settingsService,
            ILogger<OrderDisplayNumberService> logger)
        {
            _context       = context       ?? throw new ArgumentNullException(nameof(context));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger        = logger        ?? throw new ArgumentNullException(nameof(logger));
        }

        public string ResolveChannelCode(OrderType orderType, OrderSource orderSource, bool hasDeliveryPartner)
        {
            // Source wins over OrderType so that mobile/QR and delivery aggregator
            // channels never get mis-classified as DineIn/Takeaway/Delivery.
            if (orderSource == OrderSource.Mobile)
                return CodeQrMobile;
            if (orderSource == OrderSource.Online)
                return CodeOnline;

            // Partner-routed orders (Talabat / aggregator / explicit DeliveryPartner)
            // are PT regardless of OrderType. Catches the case where the cashier
            // attaches a partner to a nominal "Takeaway" order in the UI.
            if (orderSource == OrderSource.Talabat
             || orderSource == OrderSource.DeliveryPartner
             || hasDeliveryPartner)
                return CodePartner;

            return orderType switch
            {
                OrderType.Takeaway => CodeTakeaway,
                OrderType.Delivery => CodeDelivery, // in-house delivery (no partner attached)
                _                  => CodeDineIn,
            };
        }

        public async Task<string> GenerateAsync(
            Guid tenantId,
            OrderType orderType,
            OrderSource orderSource,
            bool hasDeliveryPartner,
            DateTime utcNow,
            CancellationToken ct,
            string? partnerCode = null,
            bool isOffline = false)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required.", nameof(tenantId));

            // Null config = legacy behavior (Monthly reset, PerOrderType, TA-YYYYMM-N).
            var config = await _configService.GetForTenantAsync(tenantId, ct);
            var settings = await _settingsService.GetSettingsAsync(tenantId, ct);
            var timeZone = RestaurantTimeZone.Resolve(settings.TimeZoneId);

            var channelCode = ResolveChannelCode(orderType, orderSource, hasDeliveryPartner);
            var storageCode = (config?.Scope == OrderNumberSequenceScope.Global)
                ? CodeGlobal
                : channelCode;

            // Shift lookup only happens when needed for either the reset bucket
            // or the formatted output. Skip the query entirely otherwise.
            int? shiftNumber = null;
            Guid? shiftId    = null;
            if (NeedsShiftContext(config))
            {
                var openShift = await _context.CashierBalanceShifts
                    .Where(s => s.TenantId == tenantId && s.ClosedAt == null && s.DeletedAt == null)
                    .OrderByDescending(s => s.OpenedAt)
                    .Select(s => new { s.Id, s.ShiftNumber })
                    .FirstOrDefaultAsync(ct);
                shiftId     = openShift?.Id;
                shiftNumber = openShift?.ShiftNumber;
            }

            var strategy = config?.ResetStrategy ?? OrderNumberResetStrategy.Monthly;
            var period = OrderNumberPeriodResolver.Resolve(strategy, utcNow, timeZone, shiftId);
            var assigned = await AllocateNextValueAsync(
                tenantId, storageCode, period, utcNow, ct);
            var formatted = Format(
                config, channelCode, partnerCode, isOffline, period.LocalNow, shiftNumber, assigned);

            _logger.LogInformation(
                "Allocated display order number {DisplayOrderNumber} for tenant {TenantId} (storage {StorageCode}, bucket {BucketKey})",
                formatted, tenantId, storageCode, period.BucketKey);

            return formatted;
        }

        private static bool NeedsShiftContext(OrderNumberingConfig? config)
        {
            if (config is null) return false;
            return config.IncludeShiftNumber
                || config.ResetStrategy == OrderNumberResetStrategy.ShiftOpen
                || config.ResetStrategy == OrderNumberResetStrategy.ShiftClose;
        }

        private async Task<long> AllocateNextValueAsync(
            Guid tenantId,
            string storageCode,
            OrderNumberPeriod period,
            DateTime utcNow,
            CancellationToken ct)
        {
            // Atomic upsert+increment in a single round-trip, keyed on the new
            // (TenantId, OrderTypeCode, BucketKey) unique index. Year/Month are
            // still written for historical inspection.
            //   - First write of a bucket inserts NextValue=2 and returns 1.
            //   - Subsequent writes hit ON CONFLICT, bump NextValue by 1 under
            //     the row-level lock Postgres takes for the upsert, return pre-increment.
            // Guarantees:
            //   * no duplicate numbers under concurrency (database row lock),
            //   * implicit reset to 1 when a new bucket row first appears,
            //   * previous buckets stay frozen — their rows are never touched again.
            const string sql = @"
INSERT INTO ""OrderDisplaySequences""
    (""Id"", ""TenantId"", ""OrderTypeCode"", ""Year"", ""Month"", ""BucketKey"", ""NextValue"", ""CreatedAt"", ""UpdatedAt"")
VALUES
    (@id, @tenantId, @code, @year, @month, @bucket, 2, @now, @now)
ON CONFLICT (""TenantId"", ""OrderTypeCode"", ""BucketKey"")
DO UPDATE SET
    ""NextValue"" = ""OrderDisplaySequences"".""NextValue"" + 1,
    ""UpdatedAt"" = EXCLUDED.""UpdatedAt""
RETURNING ""NextValue"" - 1;";

            var parameters = new[]
            {
                new NpgsqlParameter("@id",       Guid.NewGuid()),
                new NpgsqlParameter("@tenantId", tenantId),
                new NpgsqlParameter("@code",     storageCode),
                new NpgsqlParameter("@year",     period.LocalNow.Year),
                new NpgsqlParameter("@month",    period.LocalNow.Month),
                new NpgsqlParameter("@bucket",   period.BucketKey),
                new NpgsqlParameter("@now",      utcNow),
            };

            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            foreach (var p in parameters)
                command.Parameters.Add(p);

            var raw = await command.ExecuteScalarAsync(ct);
            if (raw is null)
                throw new InvalidOperationException("Failed to allocate display order number.");

            return Convert.ToInt64(raw);
        }

        private static string Format(
            OrderNumberingConfig? config,
            string channelCode,
            string? partnerCode,
            bool isOffline,
            DateTime localNow,
            int? shiftNumber,
            long assigned)
        {
            var parts = new List<string>(8);

            // OFF always leads when the order was synced from an offline buffer,
            // so cashiers can spot reconciled orders at a glance.
            if (isOffline) parts.Add("OFF");

            // No config → preserve the legacy "TA-202606-1" plus the new OFF/partner
            // hints when applicable.
            if (config is null)
            {
                parts.Add(channelCode);
                AddPartnerCodeIfPt(parts, channelCode, partnerCode);
                parts.Add(localNow.ToString("yyyyMM"));
                parts.Add(assigned.ToString());
                return string.Join("-", parts);
            }

            if (config.IncludeBranchCode && !string.IsNullOrWhiteSpace(config.BranchCode))
                parts.Add(config.BranchCode.Trim());

            var leadingCode = !string.IsNullOrWhiteSpace(config.Prefix)
                ? config.Prefix!.Trim()
                : config.Scope == OrderNumberSequenceScope.Global
                    ? CodeGlobalDisplay
                    : channelCode;
            parts.Add(leadingCode);

            if (config.Scope == OrderNumberSequenceScope.PerOrderType)
                AddPartnerCodeIfPt(parts, channelCode, partnerCode);

            var datePart = ResolveDatePart(config, localNow);
            if (datePart != null) parts.Add(datePart);

            if (config.IncludeShiftNumber && shiftNumber.HasValue)
                parts.Add($"S{shiftNumber.Value:D3}");

            parts.Add(assigned.ToString());

            return string.Join("-", parts);
        }

        private static string? ResolveDatePart(OrderNumberingConfig config, DateTime localNow)
        {
            if (config.IncludeDate)
                return localNow.ToString("yyyyMMdd");
            if (config.IncludeYear && config.IncludeMonth)
                return localNow.ToString("yyyyMM");
            if (config.IncludeYear)
                return localNow.ToString("yyyy");
            return config.IncludeMonth ? localNow.ToString("MM") : null;
        }

        // Embeds the partner code (Talabat, Careem, etc.) into the display number
        // only when the channel is Partner (PT) and a non-blank code is supplied.
        // Examples: "PT-TLBT-1", "OFF-PT-CAREEM-202606-1".
        private static void AddPartnerCodeIfPt(List<string> parts, string channelCode, string? partnerCode)
        {
            if (channelCode == CodePartner && !string.IsNullOrWhiteSpace(partnerCode))
                parts.Add(partnerCode.Trim());
        }
    }
}
