using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Services
{
    public sealed class StockAdjustmentService : IStockAdjustmentService
    {
        private readonly PosDbContext _context;
        private readonly IInventoryService _inventory;
        private readonly IBranchContext _branchContext;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly ILogger<StockAdjustmentService> _logger;

        public StockAdjustmentService(
            PosDbContext context,
            IInventoryService inventory,
            IBranchContext branchContext,
            ICurrentUserAccessor currentUser,
            ILogger<StockAdjustmentService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<StockAdjustmentResultDto> AdjustAsync(
            Guid materialId,
            StockAdjustmentCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var reason = dto.Reason?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(reason))
                throw new ValidationException("Adjustment reason is required.");
            if (reason.Length > 500)
                reason = reason[..500];

            // Stock precision matches the persisted decimal(18,2) column.
            var target = Math.Round(dto.NewStock, 2, MidpointRounding.AwayFromZero);
            if (target < 0)
                throw new ValidationException("New stock cannot be negative.");

            var material = await _context.RawMaterials
                .FirstOrDefaultAsync(m => m.Id == materialId, cancellationToken)
                ?? throw new NotFoundException("Raw material was not found.");

            var branchId = (await _branchContext.GetCurrentAsync(cancellationToken)).CurrentBranch.Id;
            var userId = _currentUser.UserId;
            var userName = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.FullName ?? u.Username)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            var previous = await _inventory.GetBranchStockAsync(materialId, branchId, cancellationToken);
            if (target == previous)
                throw new ValidationException("New stock equals current stock; no adjustment needed.");

            // One transaction spans the batch mutation AND the audit row so they can
            // never diverge. ApplyStockAdjustmentAsync joins this ambient transaction.
            var ownsTransaction = _context.Database.CurrentTransaction == null;
            await using var transaction = ownsTransaction
                ? await _context.Database.BeginTransactionAsync(cancellationToken)
                : null;
            try
            {
                var outcome = await _inventory.ApplyStockAdjustmentAsync(
                    materialId, target, branchId, cancellationToken);

                var log = new StockAdjustmentLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = material.TenantId,
                    BranchId = branchId,
                    MaterialId = materialId,
                    Kind = StockAdjustmentKind.StockQuantity,
                    PreviousStock = outcome.PreviousStock,
                    NewStock = outcome.NewStock,
                    AdjustmentQuantity = outcome.Delta,
                    Unit = material.Unit,
                    Reason = reason,
                    PerformedByUserId = userId,
                    PerformedByUserName = userName,
                    AdjustmentType = StockAdjustmentType.ManualSuperAdmin,
                };
                _context.StockAdjustmentLogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                if (ownsTransaction && transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                _logger.LogInformation(
                    "[StockAdjustment] Material {MaterialId} {Previous}->{New} (delta {Delta}) on branch {BranchId} by user {UserId}",
                    materialId, outcome.PreviousStock, outcome.NewStock, outcome.Delta, branchId, userId);

                return MapToDto(log, material.Name, material.NameAr);
            }
            catch
            {
                if (ownsTransaction && transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                throw;
            }
        }

        public async Task<StockAdjustmentResultDto> AdjustUnitCostAsync(
            Guid materialId,
            UnitCostAdjustmentCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var reason = dto.Reason?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(reason))
                throw new ValidationException("Adjustment reason is required.");
            if (reason.Length > 500)
                reason = reason[..500];

            // Unit cost precision matches the persisted decimal(18,3) column.
            var newUnitCost = Math.Round(dto.NewUnitCost, 3, MidpointRounding.AwayFromZero);
            if (newUnitCost <= 0)
                throw new ValidationException("New unit cost must be greater than zero.");

            var material = await _context.RawMaterials
                .FirstOrDefaultAsync(m => m.Id == materialId, cancellationToken)
                ?? throw new NotFoundException("Raw material was not found.");

            var branchId = (await _branchContext.GetCurrentAsync(cancellationToken)).CurrentBranch.Id;
            var userId = _currentUser.UserId;
            var userName = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.FullName ?? u.Username)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            var currentStock = await _inventory.GetBranchStockAsync(materialId, branchId, cancellationToken);
            var previousUnitCost = Math.Round(material.CostPerUnit, 3, MidpointRounding.AwayFromZero);
            if (newUnitCost == previousUnitCost)
                throw new ValidationException("New unit cost equals current unit cost; no change needed.");

            var ownsTransaction = _context.Database.CurrentTransaction == null;
            await using var transaction = ownsTransaction
                ? await _context.Database.BeginTransactionAsync(cancellationToken)
                : null;
            try
            {
                var outcome = await _inventory.ApplyUnitCostAdjustmentAsync(
                    materialId, newUnitCost, branchId, cancellationToken);

                var log = new StockAdjustmentLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = material.TenantId,
                    BranchId = branchId,
                    MaterialId = materialId,
                    Kind = StockAdjustmentKind.UnitCost,
                    // Stock is unchanged by a cost edit — record it for context.
                    PreviousStock = currentStock,
                    NewStock = currentStock,
                    AdjustmentQuantity = 0m,
                    PreviousUnitCost = outcome.PreviousUnitCost,
                    NewUnitCost = outcome.NewUnitCost,
                    Unit = material.Unit,
                    Reason = reason,
                    PerformedByUserId = userId,
                    PerformedByUserName = userName,
                    AdjustmentType = StockAdjustmentType.ManualSuperAdmin,
                };
                _context.StockAdjustmentLogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                if (ownsTransaction && transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                _logger.LogInformation(
                    "[UnitCostAdjustment] Material {MaterialId} {Previous}->{New} ({BatchCount} batches revalued) on branch {BranchId} by user {UserId}",
                    materialId, outcome.PreviousUnitCost, outcome.NewUnitCost, outcome.RevaluedBatchCount, branchId, userId);

                return MapToDto(log, material.Name, material.NameAr);
            }
            catch
            {
                if (ownsTransaction && transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                throw;
            }
        }

        public async Task<IReadOnlyList<StockAdjustmentResultDto>> GetHistoryAsync(
            Guid materialId,
            CancellationToken cancellationToken = default)
        {
            var branchId = (await _branchContext.GetCurrentAsync(cancellationToken)).CurrentBranch.Id;

            return await _context.StockAdjustmentLogs
                .AsNoTracking()
                .Where(a => a.MaterialId == materialId && a.BranchId == branchId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new StockAdjustmentResultDto
                {
                    Id = a.Id,
                    MaterialId = a.MaterialId,
                    MaterialName = a.RawMaterial.Name,
                    MaterialNameAr = a.RawMaterial.NameAr,
                    Kind = a.Kind.ToString(),
                    PreviousStock = a.PreviousStock,
                    NewStock = a.NewStock,
                    AdjustmentQuantity = a.AdjustmentQuantity,
                    PreviousUnitCost = a.PreviousUnitCost,
                    NewUnitCost = a.NewUnitCost,
                    Unit = a.Unit.ToString(),
                    Reason = a.Reason,
                    PerformedBy = a.PerformedByUserName,
                    CreatedAt = a.CreatedAt,
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<StockAdjustmentResultDto>> GetBranchHistoryAsync(
            CancellationToken cancellationToken = default)
        {
            var branchId = (await _branchContext.GetCurrentAsync(cancellationToken)).CurrentBranch.Id;

            return await _context.StockAdjustmentLogs
                .AsNoTracking()
                .Where(a => a.BranchId == branchId)
                .OrderByDescending(a => a.CreatedAt)
                .Take(1000)
                .Select(a => new StockAdjustmentResultDto
                {
                    Id = a.Id,
                    MaterialId = a.MaterialId,
                    MaterialName = a.RawMaterial.Name,
                    MaterialNameAr = a.RawMaterial.NameAr,
                    Kind = a.Kind.ToString(),
                    PreviousStock = a.PreviousStock,
                    NewStock = a.NewStock,
                    AdjustmentQuantity = a.AdjustmentQuantity,
                    PreviousUnitCost = a.PreviousUnitCost,
                    NewUnitCost = a.NewUnitCost,
                    Unit = a.Unit.ToString(),
                    Reason = a.Reason,
                    PerformedBy = a.PerformedByUserName,
                    CreatedAt = a.CreatedAt,
                })
                .ToListAsync(cancellationToken);
        }

        private static StockAdjustmentResultDto MapToDto(StockAdjustmentLog log, string materialName, string? materialNameAr)
            => new()
            {
                Id = log.Id,
                MaterialId = log.MaterialId,
                MaterialName = materialName,
                MaterialNameAr = materialNameAr,
                Kind = log.Kind.ToString(),
                PreviousStock = log.PreviousStock,
                NewStock = log.NewStock,
                AdjustmentQuantity = log.AdjustmentQuantity,
                PreviousUnitCost = log.PreviousUnitCost,
                NewUnitCost = log.NewUnitCost,
                Unit = log.Unit.ToString(),
                Reason = log.Reason,
                PerformedBy = log.PerformedByUserName,
                CreatedAt = log.CreatedAt,
            };
    }
}
