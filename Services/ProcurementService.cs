using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public class ProcurementService : IProcurementService
    {
        private readonly PosDbContext _context;
        private readonly IBranchContext _branchContext;
        private readonly ILogger<ProcurementService> _logger;

        public ProcurementService(
            PosDbContext context,
            IBranchContext branchContext,
            ILogger<ProcurementService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<SuggestedOrderDto>> GetSuggestedOrdersAsync()
        {
            _logger.LogInformation("[Procurement] Calculating Smart Replenishment Suggestions...");
            var branchContext = await _branchContext.GetCurrentAsync();
            var branchId = branchContext.CurrentBranch.Id;
            var isMainBranch = branchContext.CurrentBranch.IsMainBranch;

            // 1. Kritik Stok Seviyesinin Altına Düşen Hammaddeleri Bul
            var criticalMaterials = await _context.RawMaterials
                .AsNoTracking()
                .Select(rm => new
                {
                    rm.Id,
                    rm.Name,
                    rm.Unit,
                    rm.MinimumAlertLevel,
                    rm.CostPerUnit,
                    CurrentStock = _context.StockBatches.Any(sb => sb.MaterialId == rm.Id && sb.BranchId == branchId)
                        ? (_context.StockBatches
                            .Where(sb => sb.MaterialId == rm.Id
                                      && sb.BranchId == branchId
                                      && sb.IsApproved
                                      && (sb.PurchaseOrderId == null || sb.PurchaseOrder!.Status == PurchaseOrderStatus.Approved)
                                      && sb.Status == BatchStatus.Good)
                            .Sum(sb => (decimal?)sb.RemainingQuantity) ?? 0m)
                        : isMainBranch ? rm.CurrentStock : 0m
                })
                .Where(rm => rm.CurrentStock <= rm.MinimumAlertLevel)
                .ToListAsync();

            var suggestions = new List<SuggestedOrderDto>();

            foreach (var material in criticalMaterials)
            {
                // Örnek Mantık: Güvenli stok seviyesine (Min Level * 2) tamamlayacak kadar öner
                var targetStock = material.MinimumAlertLevel * 2;
                if (targetStock == 0) targetStock = 10; // Default fallback

                var quantityNeeded = targetStock - material.CurrentStock;
                
                if (quantityNeeded <= 0) continue;

                suggestions.Add(new SuggestedOrderDto
                {
                    RawMaterialId = material.Id,
                    RawMaterialName = material.Name,
                    CurrentStock = material.CurrentStock,
                    MinimumStockLevel = material.MinimumAlertLevel,
                    SuggestedQuantity = quantityNeeded,
                    Unit = material.Unit.ToString(),
                    EstimatedCost = quantityNeeded * material.CostPerUnit
                });
            }

            return suggestions.OrderByDescending(x => x.EstimatedCost).ToList();
        }
    }
}
