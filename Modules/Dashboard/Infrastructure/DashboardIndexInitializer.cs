using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RestaurantPos.Api.Data;

namespace RestaurantPos.Api.Modules.Dashboard.Infrastructure
{
    /// <summary>
    /// One-shot hosted service that ensures the dashboard-supporting indexes
    /// exist. Uses <c>CREATE INDEX IF NOT EXISTS</c> so the operation is
    /// fully idempotent and a no-op after the first deploy.
    /// </summary>
    /// <remarks>
    /// We do not ship these as an EF Core migration because the indexes are
    /// purely physical — they do not change the model. Adding them via a
    /// migration would require regenerating the entire model snapshot, which
    /// risks subtle column-ordering drift on any model change made in
    /// parallel. Running them on startup keeps the EF snapshot pristine.
    /// </remarks>
    public sealed class DashboardIndexInitializer : IHostedService
    {
        private static readonly string[] IndexSql = new[]
        {
            // Orders — the workhorse for every revenue/operations query.
            "CREATE INDEX IF NOT EXISTS ix_dash_orders_tenant_status_created ON \"Orders\" (\"TenantId\", \"Status\", \"CreatedAt\" DESC);",
            "CREATE INDEX IF NOT EXISTS ix_dash_orders_tenant_paidat        ON \"Orders\" (\"TenantId\", \"PaidAt\" DESC) WHERE \"PaidAt\" IS NOT NULL;",
            "CREATE INDEX IF NOT EXISTS ix_dash_orders_tenant_cashier_created ON \"Orders\" (\"TenantId\", \"PaidByUserId\", \"CreatedAt\" DESC);",

            // OrderItems — product mix / top sellers.
            "CREATE INDEX IF NOT EXISTS ix_dash_orderitems_order_product    ON \"OrderItems\" (\"OrderId\", \"ProductId\");",

            // Payments — cash / card split.
            "CREATE INDEX IF NOT EXISTS ix_dash_payments_order_method       ON \"Payments\" (\"OrderId\", \"Method\");",

            // Waste / Cancel / Refund — inventory + audit dashboards.
            "CREATE INDEX IF NOT EXISTS ix_dash_wastelogs_tenant_created_cat ON \"WasteLogs\" (\"TenantId\", \"CreatedAt\" DESC, \"Category\");",
            "CREATE INDEX IF NOT EXISTS ix_dash_wastelogs_tenant_wastedate_cat ON \"WasteLogs\" (\"TenantId\", \"WasteDate\" DESC, \"Category\") WHERE \"WasteDate\" IS NOT NULL;",
            "CREATE INDEX IF NOT EXISTS ix_dash_cancellog_tenant_emp_created ON \"CancelLogs\" (\"TenantId\", \"CancelledById\", \"CancelledAt\" DESC);",
            "CREATE INDEX IF NOT EXISTS ix_dash_refundlog_tenant_processed   ON \"RefundLogs\" (\"TenantId\", \"ProcessedAt\" DESC);",

            // Stock batches — \"expiring within N days\" lookup.
            "CREATE INDEX IF NOT EXISTS ix_dash_stockbatches_tenant_expiry  ON \"StockBatches\" (\"TenantId\", \"ExpiryDate\") WHERE \"RemainingQuantity\" > 0;",

            // TimeEntries — labour-cost roll-up on the financial dashboard.
            "CREATE INDEX IF NOT EXISTS ix_dash_timeentries_tenant_date     ON \"TimeEntries\" (\"TenantId\", \"Date\");"
        };

        private readonly IServiceProvider _services;
        private readonly ILogger<DashboardIndexInitializer> _logger;

        public DashboardIndexInitializer(IServiceProvider services, ILogger<DashboardIndexInitializer> logger)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _logger   = logger   ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Fire and forget — do NOT block host startup on DDL. Even though
            // CREATE INDEX IF NOT EXISTS is idempotent and fast, scheduling it
            // off the startup hot-path means a slow disk on a particular tenant
            // never delays the first request.
            _ = Task.Run(() => EnsureIndexesAsync(cancellationToken), cancellationToken);
            return Task.CompletedTask;
        }

        private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

                foreach (var sql in IndexSql)
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    try
                    {
                        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        // Never fail startup over a perf index. Just log and continue.
                        _logger.LogWarning(ex, "Dashboard index could not be created: {Sql}", sql);
                    }
                }
                _logger.LogInformation("Dashboard performance indexes ensured ({Count}).", IndexSql.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dashboard index initializer failed; dashboards still functional, just slower until indexes exist.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
