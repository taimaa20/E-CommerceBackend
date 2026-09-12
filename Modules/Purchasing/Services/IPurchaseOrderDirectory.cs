using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Purchasing.DTOs;
using RestaurantPos.Api.Modules.Purchasing.Security;

namespace RestaurantPos.Api.Modules.Purchasing.Services
{
    /// <summary>
    /// The read side of the one Purchasing screen.
    ///
    /// Two purchase documents exist and stay separate — raw materials post FIFO stock batches,
    /// finished goods post movements on the product stock ledger — because each solves a
    /// different inventory problem. This service is the seam that presents them as one list:
    /// it reads both, normalises the stage vocabulary, and pages the merged result. It writes
    /// nothing and owns no table, so the two transactional cycles are untouched by it.
    /// </summary>
    public interface IPurchaseOrderDirectory
    {
        Task<PaginatedResponse<PurchaseOrderRowDto>> GetPageAsync(
            Guid branchId,
            PurchaseOrderDirectoryQuery query,
            PurchasingVisibility visibility,
            bool isArabic,
            CancellationToken ct = default);
    }
}
