using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public class WarehouseService : IWarehouseService
    {
        private readonly PosDbContext _context;
        private readonly IBranchContext _branchContext;
        private readonly ILogger<WarehouseService> _logger;

        public WarehouseService(
            PosDbContext context,
            IBranchContext branchContext,
            ILogger<WarehouseService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<WarehouseDto>> GetAllAsync(bool isArabic, CancellationToken ct)
        {
            return await _context.Warehouses
                .AsNoTracking()
                .OrderByDescending(w => w.Type == WarehouseType.Main)
                .ThenBy(w => w.Name)
                .Select(w => Map(w, isArabic))
                .ToListAsync(ct);
        }

        public async Task<WarehouseDto?> GetByIdAsync(Guid id, bool isArabic, CancellationToken ct)
        {
            var w = await _context.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            return w == null ? null : Map(w, isArabic);
        }

        public async Task<WarehouseDto> CreateAsync(WarehouseCreateDto dto, Guid tenantId, bool isArabic, CancellationToken ct)
        {
            var code = dto.Code.Trim();
            var codeExists = await _context.Warehouses.AnyAsync(w => w.Code == code, ct);
            if (codeExists)
                throw new InvalidOperationException($"Warehouse code '{code}' is already in use.");

            var type = (WarehouseType)dto.Type;
            if (type == WarehouseType.Main)
            {
                var hasMain = await _context.Warehouses.AnyAsync(w => w.Type == WarehouseType.Main, ct);
                if (hasMain)
                    throw new InvalidOperationException("Only one Main warehouse is allowed per tenant.");
            }

            var entity = new Warehouse
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = dto.Name.Trim(),
                NameAr = string.IsNullOrWhiteSpace(dto.NameAr) ? null : dto.NameAr.Trim(),
                Type = type,
                IsActive = dto.IsActive
            };

            _context.Warehouses.Add(entity);
            await _context.SaveChangesAsync(ct);
            return Map(entity, isArabic);
        }

        public async Task<WarehouseDto> UpdateAsync(Guid id, WarehouseUpdateDto dto, bool isArabic, CancellationToken ct)
        {
            var entity = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == id, ct)
                ?? throw new InvalidOperationException("Warehouse not found.");

            var newCode = dto.Code.Trim();
            if (!string.Equals(entity.Code, newCode, StringComparison.OrdinalIgnoreCase))
            {
                var codeExists = await _context.Warehouses.AnyAsync(w => w.Code == newCode && w.Id != id, ct);
                if (codeExists)
                    throw new InvalidOperationException($"Warehouse code '{newCode}' is already in use.");
                entity.Code = newCode;
            }

            var newType = (WarehouseType)dto.Type;
            if (entity.Type != WarehouseType.Main && newType == WarehouseType.Main)
            {
                var hasMain = await _context.Warehouses.AnyAsync(w => w.Type == WarehouseType.Main && w.Id != id, ct);
                if (hasMain)
                    throw new InvalidOperationException("Only one Main warehouse is allowed per tenant.");
            }

            entity.Name = dto.Name.Trim();
            entity.NameAr = string.IsNullOrWhiteSpace(dto.NameAr) ? null : dto.NameAr.Trim();
            entity.Type = newType;
            entity.IsActive = dto.IsActive;

            await _context.SaveChangesAsync(ct);
            return Map(entity, isArabic);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            var entity = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == id, ct)
                ?? throw new InvalidOperationException("Warehouse not found.");

            if (entity.Type == WarehouseType.Main)
                throw new InvalidOperationException("The Main warehouse cannot be deleted.");

            var hasStock = await _context.RawMaterialInventories
                .AnyAsync(i => i.WarehouseId == id && i.Quantity > 0, ct);
            if (hasStock)
                throw new InvalidOperationException("Warehouse still has stock — transfer it out before deleting.");

            entity.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        public async Task<List<RawMaterialInventoryDto>> GetInventoryAsync(Guid? warehouseId, Guid? rawMaterialId, bool isArabic, CancellationToken ct)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);
            var query = _context.RawMaterialInventories
                .AsNoTracking()
                .Include(i => i.RawMaterial)
                .Include(i => i.Warehouse)
                .Where(i => i.BranchId == branchId)
                .AsQueryable();

            if (warehouseId.HasValue) query = query.Where(i => i.WarehouseId == warehouseId.Value);
            if (rawMaterialId.HasValue) query = query.Where(i => i.RawMaterialId == rawMaterialId.Value);

            return await query
                .OrderBy(i => i.Warehouse!.Name)
                .ThenBy(i => i.RawMaterial!.Name)
                .Select(i => new RawMaterialInventoryDto
                {
                    Id = i.Id,
                    RawMaterialId = i.RawMaterialId,
                    RawMaterialName = i.RawMaterial!.Name,
                    RawMaterialNameAr = i.RawMaterial.NameAr,
                    WarehouseId = i.WarehouseId,
                    WarehouseCode = i.Warehouse!.Code,
                    WarehouseName = i.Warehouse.Name,
                    WarehouseNameAr = i.Warehouse.NameAr,
                    WarehouseType = i.Warehouse.Type,
                    Quantity = i.Quantity,
                    MinimumQuantity = i.MinimumQuantity,
                    ReorderLevel = i.ReorderLevel,
                    Unit = i.RawMaterial.Unit.ToString()
                })
                .ToListAsync(ct);
        }

        public async Task<RawMaterialInventoryDto> UpsertInventoryAsync(RawMaterialInventoryUpsertDto dto, Guid tenantId, bool isArabic, CancellationToken ct)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);
            var existing = await _context.RawMaterialInventories
                .Include(i => i.RawMaterial)
                .Include(i => i.Warehouse)
                .FirstOrDefaultAsync(i => i.RawMaterialId == dto.RawMaterialId && i.WarehouseId == dto.WarehouseId && i.BranchId == branchId, ct);

            if (existing == null)
            {
                existing = new RawMaterialInventory
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = branchId,
                    RawMaterialId = dto.RawMaterialId,
                    WarehouseId = dto.WarehouseId
                };
                _context.RawMaterialInventories.Add(existing);
            }

            existing.Quantity = dto.Quantity;
            existing.MinimumQuantity = dto.MinimumQuantity;
            existing.ReorderLevel = dto.ReorderLevel;

            await _context.SaveChangesAsync(ct);

            existing = await _context.RawMaterialInventories
                .AsNoTracking()
                .Include(i => i.RawMaterial)
                .Include(i => i.Warehouse)
                .FirstAsync(i => i.Id == existing.Id, ct);

            return new RawMaterialInventoryDto
            {
                Id = existing.Id,
                RawMaterialId = existing.RawMaterialId,
                RawMaterialName = existing.RawMaterial!.Name,
                RawMaterialNameAr = existing.RawMaterial.NameAr,
                WarehouseId = existing.WarehouseId,
                WarehouseCode = existing.Warehouse!.Code,
                WarehouseName = existing.Warehouse.Name,
                WarehouseNameAr = existing.Warehouse.NameAr,
                WarehouseType = existing.Warehouse.Type,
                Quantity = existing.Quantity,
                MinimumQuantity = existing.MinimumQuantity,
                ReorderLevel = existing.ReorderLevel,
                Unit = existing.RawMaterial.Unit.ToString()
            };
        }

        public async Task<InventoryTransferDto> CreateTransferAsync(
            InventoryTransferCreateDto dto, Guid tenantId, Guid? performedById, bool isArabic, CancellationToken ct)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);
            if (dto.FromWarehouseId == dto.ToWarehouseId)
                throw new InvalidOperationException("Source and destination warehouses must differ.");
            if (dto.Quantity <= 0)
                throw new InvalidOperationException("Transfer quantity must be greater than zero.");

            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                var fromWh = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == dto.FromWarehouseId, ct)
                    ?? throw new InvalidOperationException("Source warehouse not found.");
                var toWh = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == dto.ToWarehouseId, ct)
                    ?? throw new InvalidOperationException("Destination warehouse not found.");
                if (!fromWh.IsActive || !toWh.IsActive)
                    throw new InvalidOperationException("Both warehouses must be active.");

                var material = await _context.RawMaterials.FirstOrDefaultAsync(m => m.Id == dto.RawMaterialId, ct)
                    ?? throw new InvalidOperationException("Raw material not found.");

                var source = await _context.RawMaterialInventories
                    .FirstOrDefaultAsync(i => i.RawMaterialId == dto.RawMaterialId && i.WarehouseId == dto.FromWarehouseId && i.BranchId == branchId, ct);

                if (source == null || source.Quantity < dto.Quantity)
                    throw new InvalidOperationException("Insufficient stock at the source warehouse.");

                var destination = await _context.RawMaterialInventories
                    .FirstOrDefaultAsync(i => i.RawMaterialId == dto.RawMaterialId && i.WarehouseId == dto.ToWarehouseId && i.BranchId == branchId, ct);

                if (destination == null)
                {
                    destination = new RawMaterialInventory
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        BranchId = branchId,
                        RawMaterialId = dto.RawMaterialId,
                        WarehouseId = dto.ToWarehouseId,
                        Quantity = 0m,
                        MinimumQuantity = 0m,
                        ReorderLevel = 0m
                    };
                    _context.RawMaterialInventories.Add(destination);
                }

                source.Quantity -= dto.Quantity;
                destination.Quantity += dto.Quantity;

                var transfer = new InventoryTransfer
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = branchId,
                    FromWarehouseId = dto.FromWarehouseId,
                    ToWarehouseId = dto.ToWarehouseId,
                    RawMaterialId = dto.RawMaterialId,
                    Quantity = dto.Quantity,
                    Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                    PerformedById = performedById,
                    Status = InventoryTransferStatus.Completed
                };
                _context.InventoryTransfers.Add(transfer);

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                _logger.LogInformation(
                    "Transfer {TransferId}: {Qty} of {Material} from {From} to {To} by {User}",
                    transfer.Id, dto.Quantity, material.Name, fromWh.Name, toWh.Name, performedById);

                return new InventoryTransferDto
                {
                    Id = transfer.Id,
                    FromWarehouseId = fromWh.Id,
                    FromWarehouseName = fromWh.Name,
                    ToWarehouseId = toWh.Id,
                    ToWarehouseName = toWh.Name,
                    RawMaterialId = material.Id,
                    RawMaterialName = material.Name,
                    RawMaterialNameAr = material.NameAr,
                    Quantity = transfer.Quantity,
                    Unit = material.Unit.ToString(),
                    Notes = transfer.Notes,
                    PerformedById = transfer.PerformedById,
                    Status = transfer.Status,
                    CreatedAt = transfer.CreatedAt
                };
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<List<InventoryTransferDto>> GetTransfersAsync(
            Guid? warehouseId, Guid? rawMaterialId, DateTime? from, DateTime? to, bool isArabic, CancellationToken ct)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);
            var query = _context.InventoryTransfers
                .AsNoTracking()
                .Include(t => t.FromWarehouse)
                .Include(t => t.ToWarehouse)
                .Include(t => t.RawMaterial)
                .Include(t => t.PerformedBy)
                .Where(t => t.BranchId == branchId)
                .AsQueryable();

            if (warehouseId.HasValue)
                query = query.Where(t => t.FromWarehouseId == warehouseId.Value || t.ToWarehouseId == warehouseId.Value);
            if (rawMaterialId.HasValue)
                query = query.Where(t => t.RawMaterialId == rawMaterialId.Value);
            if (from.HasValue) query = query.Where(t => t.CreatedAt >= from.Value);
            if (to.HasValue) query = query.Where(t => t.CreatedAt <= to.Value);

            return await query
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new InventoryTransferDto
                {
                    Id = t.Id,
                    FromWarehouseId = t.FromWarehouseId,
                    FromWarehouseName = t.FromWarehouse!.Name,
                    ToWarehouseId = t.ToWarehouseId,
                    ToWarehouseName = t.ToWarehouse!.Name,
                    RawMaterialId = t.RawMaterialId,
                    RawMaterialName = t.RawMaterial!.Name,
                    RawMaterialNameAr = t.RawMaterial.NameAr,
                    Quantity = t.Quantity,
                    Unit = t.RawMaterial.Unit.ToString(),
                    Notes = t.Notes,
                    PerformedById = t.PerformedById,
                    PerformedByName = t.PerformedBy != null ? t.PerformedBy.Username : null,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync(ct);
        }

        public async Task<List<StockByWarehouseRowDto>> GetStockByWarehouseAsync(Guid? warehouseId, bool lowOnly, bool isArabic, CancellationToken ct)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);
            var query = _context.RawMaterialInventories
                .AsNoTracking()
                .Include(i => i.RawMaterial)
                .Include(i => i.Warehouse)
                .Where(i => i.BranchId == branchId)
                .AsQueryable();

            if (warehouseId.HasValue) query = query.Where(i => i.WarehouseId == warehouseId.Value);

            var rows = await query
                .Select(i => new StockByWarehouseRowDto
                {
                    WarehouseId = i.WarehouseId,
                    WarehouseName = i.Warehouse!.Name,
                    WarehouseCode = i.Warehouse.Code,
                    RawMaterialId = i.RawMaterialId,
                    RawMaterialName = i.RawMaterial!.Name,
                    RawMaterialNameAr = i.RawMaterial.NameAr,
                    Unit = i.RawMaterial.Unit.ToString(),
                    Quantity = i.Quantity,
                    MinimumQuantity = i.MinimumQuantity,
                    ReorderLevel = i.ReorderLevel,
                    IsLow = i.MinimumQuantity > 0 && i.Quantity <= i.MinimumQuantity
                })
                .OrderBy(r => r.WarehouseName)
                .ThenBy(r => r.RawMaterialName)
                .ToListAsync(ct);

            return lowOnly ? rows.Where(r => r.IsLow).ToList() : rows;
        }

        public async Task<Guid?> ResolveSourceWarehouseIdAsync(Guid rawMaterialId, CancellationToken ct)
        {
            var defaultId = await _context.RawMaterials
                .AsNoTracking()
                .Where(m => m.Id == rawMaterialId)
                .Select(m => m.DefaultWarehouseId)
                .FirstOrDefaultAsync(ct);

            if (defaultId.HasValue)
            {
                var stillActive = await _context.Warehouses
                    .AsNoTracking()
                    .AnyAsync(w => w.Id == defaultId.Value && w.IsActive, ct);
                if (stillActive) return defaultId;
            }

            var mainId = await _context.Warehouses
                .AsNoTracking()
                .Where(w => w.Type == WarehouseType.Main && w.IsActive)
                .Select(w => (Guid?)w.Id)
                .FirstOrDefaultAsync(ct);

            return mainId;
        }

        public async Task DecrementInventoryAsync(Guid rawMaterialId, Guid warehouseId, decimal quantity, CancellationToken ct)
            => await DecrementInventoryAsync(rawMaterialId, warehouseId, quantity, await GetCurrentBranchIdAsync(ct), ct);

        public async Task DecrementInventoryAsync(Guid rawMaterialId, Guid warehouseId, decimal quantity, Guid branchId, CancellationToken ct)
        {
            if (quantity <= 0) return;

            var row = await _context.RawMaterialInventories
                .FirstOrDefaultAsync(i => i.RawMaterialId == rawMaterialId && i.WarehouseId == warehouseId && i.BranchId == branchId, ct);

            if (row == null) return; // legacy material: nothing to track at warehouse level

            row.Quantity -= quantity;
            if (row.Quantity < 0) row.Quantity = 0;
            await _context.SaveChangesAsync(ct);
        }

        public async Task IncrementInventoryAsync(Guid rawMaterialId, Guid warehouseId, decimal quantity, CancellationToken ct)
            => await IncrementInventoryAsync(rawMaterialId, warehouseId, quantity, await GetCurrentBranchIdAsync(ct), ct);

        public async Task IncrementInventoryAsync(Guid rawMaterialId, Guid warehouseId, decimal quantity, Guid branchId, CancellationToken ct)
        {
            if (quantity <= 0) return;

            var row = await _context.RawMaterialInventories
                .FirstOrDefaultAsync(i => i.RawMaterialId == rawMaterialId && i.WarehouseId == warehouseId && i.BranchId == branchId, ct);

            if (row == null)
            {
                // First receipt for this (branch, material, warehouse): create the
                // ledger row. Only the Main Branch is pre-seeded by WarehouseBackfill,
                // so without this a non-Main branch could never accumulate ledger
                // quantity and its warehouse screens/dashboards would read zero.
                var material = await _context.RawMaterials
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == rawMaterialId, ct);
                if (material == null) return;

                _context.RawMaterialInventories.Add(new RawMaterialInventory
                {
                    Id = Guid.NewGuid(),
                    TenantId = material.TenantId,
                    BranchId = branchId,
                    RawMaterialId = rawMaterialId,
                    WarehouseId = warehouseId,
                    Quantity = quantity,
                    MinimumQuantity = material.MinimumAlertLevel,
                    ReorderLevel = material.MinimumAlertLevel
                });
                await _context.SaveChangesAsync(ct);
                return;
            }

            row.Quantity += quantity;
            await _context.SaveChangesAsync(ct);
        }

        private async Task<Guid> GetCurrentBranchIdAsync(CancellationToken ct)
            => (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;

        private static WarehouseDto Map(Warehouse w, bool isArabic) => new()
        {
            Id = w.Id,
            Code = w.Code,
            Name = w.Name,
            NameAr = w.NameAr,
            DisplayName = isArabic && !string.IsNullOrWhiteSpace(w.NameAr) ? w.NameAr! : w.Name,
            Type = w.Type,
            IsActive = w.IsActive
        };
    }
}
