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
    public interface ILowStockAlertJobService
    {
        Task<LowStockAlertJobResult> RunAsync(CancellationToken cancellationToken = default);
    }

    public sealed class LowStockAlertJobResult
    {
        public string Message { get; init; } = string.Empty;
        public int MaterialsNotified { get; init; }
        public bool SkippedBecauseAlreadyRunning { get; init; }
    }

    internal sealed record LowStockCandidate(
        Guid MaterialId,
        Guid TenantId,
        string Name,
        string Unit,
        decimal MinimumAlertLevel,
        Guid? BranchId,
        decimal Stock);

    /// <summary>
    /// Scans every tenant's raw materials and raises a single
    /// <see cref="NotificationType.LowStock"/> notification per material per branch whose
    /// active stock is less than or equal to MinimumAlertLevel. Stock is evaluated per branch —
    /// a shortage in one branch is never masked by surplus in another. Duplicate notifications
    /// created earlier today for the same material and branch are suppressed.
    /// Runs before the kitchen opens so managers see a fresh reorder list in the morning.
    /// </summary>
    public class LowStockAlertJobService : ILowStockAlertJobService
    {
        private const long AdvisoryLockKey = 2026042004;
        private const string NotifyRoles = "Admin,Manager";

        private readonly PosDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly ILogger<LowStockAlertJobService> _logger;

        public LowStockAlertJobService(
            PosDbContext context,
            INotificationService notificationService,
            ILogger<LowStockAlertJobService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<LowStockAlertJobResult> RunAsync(CancellationToken cancellationToken = default)
        {
            var lockAcquired = await TryAcquireLockAsync(cancellationToken);
            if (!lockAcquired)
            {
                await _context.Database.CloseConnectionAsync();
                _logger.LogInformation("[LowStockAlert] Skipped — another runner holds the advisory lock.");
                return new LowStockAlertJobResult
                {
                    Message = "Low-stock alert already running.",
                    SkippedBecauseAlreadyRunning = true
                };
            }

            try
            {
                var todayStart = DateTime.UtcNow.Date;

                var materials = await _context.RawMaterials
                    .IgnoreQueryFilters()
                    .Where(m => m.MinimumAlertLevel > 0m)
                    .Select(m => new { m.Id, m.TenantId, m.Name, m.Unit, m.MinimumAlertLevel, m.CurrentStock })
                    .ToListAsync(cancellationToken);

                if (materials.Count == 0)
                {
                    return new LowStockAlertJobResult { Message = "No low-stock materials." };
                }

                // Per-branch stock, one row per (material, branch) that ever had a batch.
                // Evaluating each branch separately keeps one branch's shortage visible even
                // when another branch holds plenty of the same material.
                var branchStocks = await _context.StockBatches
                    .IgnoreQueryFilters()
                    .GroupBy(sb => new { sb.MaterialId, sb.BranchId })
                    .Select(g => new
                    {
                        g.Key.MaterialId,
                        g.Key.BranchId,
                        Stock = g
                            .Where(sb => sb.IsApproved
                                      && (sb.PurchaseOrderId == null || sb.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                                      && sb.Status == BatchStatus.Good)
                            .Sum(sb => (decimal?)sb.RemainingQuantity) ?? 0m
                    })
                    .ToListAsync(cancellationToken);

                var stocksByMaterial = branchStocks.ToLookup(s => s.MaterialId);
                var mainBranchByTenant = await _context.Branches
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(b => b.IsMainBranch)
                    .ToDictionaryAsync(b => b.TenantId, b => b.Id, cancellationToken);

                var candidates = new List<LowStockCandidate>();
                foreach (var material in materials)
                {
                    var entries = stocksByMaterial[material.Id].ToList();
                    if (entries.Count == 0)
                    {
                        // Legacy materials without any batch history: fall back to the
                        // CurrentStock mirror, which belongs to the Main Branch.
                        if (material.CurrentStock <= material.MinimumAlertLevel)
                        {
                            candidates.Add(new LowStockCandidate(
                                material.Id, material.TenantId, material.Name, material.Unit.ToString(),
                                material.MinimumAlertLevel,
                                mainBranchByTenant.TryGetValue(material.TenantId, out var mainId) ? mainId : null,
                                material.CurrentStock));
                        }
                        continue;
                    }

                    foreach (var entry in entries)
                    {
                        if (entry.Stock <= material.MinimumAlertLevel)
                        {
                            candidates.Add(new LowStockCandidate(
                                material.Id, material.TenantId, material.Name, material.Unit.ToString(),
                                material.MinimumAlertLevel, entry.BranchId, entry.Stock));
                        }
                    }
                }

                if (candidates.Count == 0)
                {
                    return new LowStockAlertJobResult { Message = "No low-stock materials." };
                }

                // One query to find which (material, branch) pairs were already alerted today.
                var materialIds = candidates.Select(c => c.MaterialId.ToString()).Distinct().ToList();
                var alreadyAlerted = (await _context.Notifications
                    .IgnoreQueryFilters()
                    .Where(n => n.Type == NotificationType.LowStock
                             && n.CreatedAt >= todayStart
                             && n.ReferenceId != null
                             && materialIds.Contains(n.ReferenceId))
                    .Select(n => new { n.ReferenceId, n.BranchId })
                    .ToListAsync(cancellationToken))
                    .Select(n => (n.ReferenceId!, n.BranchId))
                    .ToHashSet();

                var branchIds = candidates
                    .Where(c => c.BranchId.HasValue)
                    .Select(c => c.BranchId!.Value)
                    .Distinct()
                    .ToList();
                var branchNames = await _context.Branches
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(b => branchIds.Contains(b.Id))
                    .Select(b => new { b.Id, b.Name, b.IsMainBranch })
                    .ToDictionaryAsync(b => b.Id, cancellationToken);

                var notified = 0;
                foreach (var candidate in candidates)
                {
                    var referenceId = candidate.MaterialId.ToString();
                    if (candidate.BranchId.HasValue
                        && alreadyAlerted.Contains((referenceId, candidate.BranchId.Value)))
                        continue;

                    // Main Branch messages keep the exact legacy wording; other branches
                    // carry the branch name so the reorder list is actionable per location.
                    var isMainBranch = !candidate.BranchId.HasValue
                        || (branchNames.TryGetValue(candidate.BranchId.Value, out var branch) && branch.IsMainBranch);
                    var branchSuffix = isMainBranch || candidate.BranchId is null
                        ? string.Empty
                        : $" ({branchNames[candidate.BranchId.Value].Name})";

                    _ = _notificationService.SendAsync(
                        NotificationType.LowStock,
                        $"{candidate.Name} is low on stock",
                        $"{candidate.Name}{branchSuffix}: {candidate.Stock:F2} {candidate.Unit} remaining (alert level {candidate.MinimumAlertLevel:F2}).",
                        referenceId,
                        NotifyRoles,
                        candidate.TenantId,
                        candidate.BranchId);

                    notified++;
                }

                _logger.LogInformation("[LowStockAlert] Notified {Count} material(s).", notified);

                return new LowStockAlertJobResult
                {
                    Message = "Low-stock alert completed.",
                    MaterialsNotified = notified
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
                _logger.LogWarning(ex, "[LowStockAlert] Failed to release advisory lock.");
            }
        }
    }
}
