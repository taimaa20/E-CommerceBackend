using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public interface IShiftSummaryService
    {
        /// <summary>
        /// Returns the live summary for a single shift. Pass shiftId=null to use
        /// the current user's own active shift; throws NotFound otherwise.
        /// </summary>
        Task<ShiftSummaryDto> GetSummaryAsync(
            Guid tenantId,
            Guid? shiftId,
            Guid currentUserId,
            bool isAdmin,
            bool isArabic,
            CancellationToken ct);
    }
}
