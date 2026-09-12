using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    public class ExpiryJobService : IExpiryJobService
    {
        private const long AdvisoryLockKey = 2026042001;
        private const string DefaultNotifyRoles = "Admin,Manager";
        private const string AutoExpiryReason = "Auto-detected expiry | انتهاء صلاحية تلقائي";

        private readonly PosDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<ExpiryJobService> _logger;

        public ExpiryJobService(
            PosDbContext context,
            INotificationService notificationService,
            ISettingsService settingsService,
            ILogger<ExpiryJobService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ExpiryJobRunResult> RunAsync(CancellationToken cancellationToken = default)
        {
            var lockAcquired = await TryAcquireLockAsync(cancellationToken);
            if (!lockAcquired)
            {
                await _context.Database.CloseConnectionAsync();
                _logger.LogInformation("[ExpiryJob] Skipped because another runner already holds the advisory lock.");
                return new ExpiryJobRunResult
                {
                    Message = "Expiry check is already running.",
                    SkippedBecauseAlreadyRunning = true
                };
            }

            try
            {
                var settings = await _settingsService.GetSettingsAsync(cancellationToken: cancellationToken);
                var today = DateTime.UtcNow.Date;
                var nearExpiryDays = Math.Clamp(settings.NearExpiryDays, 1, 30);
                var notifyRoles = string.IsNullOrWhiteSpace(settings.ExpiryNotifyRoles)
                    ? DefaultNotifyRoles
                    : settings.ExpiryNotifyRoles.Trim();
                var nearExpiryThreshold = today.AddDays(nearExpiryDays);

                var expiredLogged = 0;
                var nearExpiryNotified = 0;
                var affectedMaterialIds = new HashSet<Guid>();

                // <= today: a batch whose ExpiryDate is today is treated as expired on today's
                // 01:00 UTC run, so the waste log reflects reality same-day (was `< today`).
                var expiredBatches = await _context.StockBatches
                    .IgnoreQueryFilters()
                    .Include(sb => sb.RawMaterial)
                    .Where(sb => sb.IsApproved
                              && (sb.PurchaseOrderId == null || sb.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                              && sb.ExpiryDate <= today
                              && sb.ExpiredWasteLogged == false
                              && sb.RemainingQuantity > 0
                              && sb.Status != BatchStatus.Finished)
                    .ToListAsync(cancellationToken);

                var mainBranchIds = expiredBatches.Count == 0
                    ? new HashSet<Guid>()
                    : (await _context.Branches
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .Where(branch => branch.IsMainBranch)
                        .Select(branch => branch.Id)
                        .ToListAsync(cancellationToken))
                        .ToHashSet();

                foreach (var batch in expiredBatches)
                {
                    var material = batch.RawMaterial;
                    var costAmount = batch.RemainingQuantity * batch.UnitCost;

                    _context.WasteLogs.Add(new WasteLog
                    {
                        Id = Guid.NewGuid(),
                        TenantId = batch.TenantId,
                        BranchId = batch.BranchId,
                        Type = WasteLogType.Waste,
                        Category = WasteCategory.ExpiryProduct,
                        ItemId = batch.MaterialId,
                        MaterialId = batch.MaterialId,
                        ItemName = material?.Name ?? "Unknown",
                        ItemNameAr = material?.NameAr,
                        Quantity = batch.RemainingQuantity,
                        Amount = costAmount,
                        Unit = material?.Unit.ToString() ?? string.Empty,
                        Reason = AutoExpiryReason,
                        CostAmount = costAmount,
                        SalePriceLoss = 0m,
                        Status = WasteLogStatus.Approved,
                        LoggedById = null,
                        LoggedByName = null,
                        IsAffectingInventory = true,
                        SourceBatchId = batch.Id,
                        SourceBatchNumber = batch.BatchNumber, // snapshot — survives batch deletion
                        CreatedAt = DateTime.UtcNow
                    });

                    batch.ExpiredWasteLogged = true;
                    batch.Status = BatchStatus.Expired;
                    if (mainBranchIds.Contains(batch.BranchId))
                        affectedMaterialIds.Add(batch.MaterialId);
                    expiredLogged++;

                    _logger.LogInformation("[ExpiryJob] Logged expired batch {BatchId} for tenant {TenantId} and material {MaterialId}", batch.Id, batch.TenantId, batch.MaterialId);

                    var itemName = material?.Name ?? "Unknown";
                    _ = _notificationService.SendAsync(
                        NotificationType.ItemExpired,
                        $"{itemName} expired and logged as waste",
                        $"{itemName} (batch {batch.BatchNumber}) expired on {batch.ExpiryDate:yyyy-MM-dd}. Qty: {batch.RemainingQuantity:F2} {material?.Unit}",
                        batch.MaterialId.ToString(),
                        notifyRoles,
                        batch.TenantId,
                        batch.BranchId);
                }

                if (affectedMaterialIds.Count > 0)
                {
                    var materials = await _context.RawMaterials
                        .IgnoreQueryFilters()
                        .Where(m => affectedMaterialIds.Contains(m.Id))
                        .ToListAsync(cancellationToken);

                    foreach (var material in materials)
                    {
                        material.CurrentStock = await _context.StockBatches
                            .IgnoreQueryFilters()
                            .Where(batch => batch.MaterialId == material.Id
                                         && mainBranchIds.Contains(batch.BranchId)
                                         && batch.IsApproved
                                         && (batch.PurchaseOrderId == null || batch.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                                         && batch.Status == BatchStatus.Good)
                            .SumAsync(batch => batch.RemainingQuantity, cancellationToken);
                    }
                }

                var nearExpiryBatches = await _context.StockBatches
                    .IgnoreQueryFilters()
                    .Include(sb => sb.RawMaterial)
                    .Where(sb => sb.IsApproved
                              && (sb.PurchaseOrderId == null || sb.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                              && sb.ExpiryDate >= today
                              && sb.ExpiryDate <= nearExpiryThreshold
                              && sb.NearExpiryNotified == false
                              && sb.RemainingQuantity > 0
                              && sb.Status == BatchStatus.Good)
                    .ToListAsync(cancellationToken);

                foreach (var batch in nearExpiryBatches)
                {
                    var material = batch.RawMaterial;
                    var daysLeft = (batch.ExpiryDate.Date - today).Days;
                    var itemName = material?.Name ?? "Unknown";

                    batch.NearExpiryNotified = true;
                    nearExpiryNotified++;

                    _ = _notificationService.SendAsync(
                        NotificationType.ItemNearExpiry,
                        $"{itemName} expires in {daysLeft} day(s)",
                        $"{itemName} (batch {batch.BatchNumber}) expires on {batch.ExpiryDate:yyyy-MM-dd}. Qty: {batch.RemainingQuantity:F2} {material?.Unit}",
                        batch.MaterialId.ToString(),
                        notifyRoles,
                        batch.TenantId,
                        batch.BranchId);

                    _logger.LogInformation("[ExpiryJob] Near-expiry notification sent for batch {BatchId} in tenant {TenantId}", batch.Id, batch.TenantId);
                }

                await _context.SaveChangesAsync(cancellationToken);

                return new ExpiryJobRunResult
                {
                    Message = "Expiry check completed.",
                    ExpiredLogged = expiredLogged,
                    NearExpiryNotified = nearExpiryNotified
                };
            }
            finally
            {
                await ReleaseLockAsync();
                await _context.Database.CloseConnectionAsync();
            }
        }

        private async Task<bool> TryAcquireLockAsync(CancellationToken cancellationToken)
        {
            await _context.Database.OpenConnectionAsync(cancellationToken);

            await using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT pg_try_advisory_lock({AdvisoryLockKey})";

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is bool acquired && acquired;
        }

        private async Task ReleaseLockAsync()
        {
            if (_context.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
            {
                return;
            }

            try
            {
                await using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = $"SELECT pg_advisory_unlock({AdvisoryLockKey})";
                await command.ExecuteScalarAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ExpiryJob] Failed to release advisory lock.");
            }
        }
    }
}
