using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Data
{
    /// <summary>
    /// Idempotent post-migration backfill. Ensures every tenant has a Main
    /// warehouse and a RawMaterialInventory row per existing RawMaterial.
    /// Runs once at startup right after <c>Database.Migrate()</c>. Safe to
    /// re-run on every deploy — does nothing when data already matches.
    /// </summary>
    public static class WarehouseBackfill
    {
        public static async Task RunAsync(PosDbContext context, CancellationToken ct = default)
        {
            // IgnoreQueryFilters: we need to see every tenant's data here.
            var tenantIds = await context.RawMaterials
                .IgnoreQueryFilters()
                .Where(m => m.DeletedAt == null)
                .Select(m => m.TenantId)
                .Distinct()
                .ToListAsync(ct);

            foreach (var tenantId in tenantIds)
            {
                var mainId = await context.Warehouses
                    .IgnoreQueryFilters()
                    .Where(w => w.TenantId == tenantId && w.DeletedAt == null && w.Type == WarehouseType.Main)
                    .Select(w => (Guid?)w.Id)
                    .FirstOrDefaultAsync(ct);

                if (!mainId.HasValue)
                {
                    var main = new Warehouse
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Code = "MAIN",
                        Name = "Main Warehouse",
                        NameAr = "المستودع الرئيسي",
                        Type = WarehouseType.Main,
                        IsActive = true
                    };

                    context.Warehouses.Add(main);
                    await context.SaveChangesAsync(ct);
                    mainId = main.Id;
                }

                var mainBranchId = await context.Branches
                    .IgnoreQueryFilters()
                    .Where(b => b.TenantId == tenantId && b.IsMainBranch)
                    .Select(b => (Guid?)b.Id)
                    .FirstOrDefaultAsync(ct);
                if (!mainBranchId.HasValue)
                    continue;

                // Track entities (no AsNoTracking) so we can mutate
                // DefaultWarehouseId below in the same change set.
                var materials = await context.RawMaterials
                    .IgnoreQueryFilters()
                    .Where(m => m.TenantId == tenantId && m.DeletedAt == null)
                    .ToListAsync(ct);

                var alreadySeededIds = await context.RawMaterialInventories
                    .IgnoreQueryFilters()
                    .Where(i => i.TenantId == tenantId && i.BranchId == mainBranchId.Value && i.WarehouseId == mainId.Value && i.DeletedAt == null)
                    .Select(i => i.RawMaterialId)
                    .ToListAsync(ct);

                var seededSet = alreadySeededIds.ToHashSet();

                foreach (var m in materials)
                {
                    // Point legacy materials at the tenant's Main warehouse.
                    // Never overwrite an explicit assignment.
                    if (!m.DefaultWarehouseId.HasValue)
                    {
                        m.DefaultWarehouseId = mainId.Value;
                    }

                    if (!seededSet.Contains(m.Id))
                    {
                        context.RawMaterialInventories.Add(new RawMaterialInventory
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            BranchId = mainBranchId.Value,
                            RawMaterialId = m.Id,
                            WarehouseId = mainId.Value,
                            Quantity = m.CurrentStock,
                            MinimumQuantity = m.MinimumAlertLevel,
                            ReorderLevel = m.MinimumAlertLevel
                        });
                    }
                }

                if (context.ChangeTracker.HasChanges())
                {
                    await context.SaveChangesAsync(ct);
                }
            }
        }
    }
}
