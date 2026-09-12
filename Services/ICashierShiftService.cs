using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public interface ICashierShiftService
    {
        Task<List<CashierBalanceShiftDto>> GetShiftsAsync(
            Guid tenantId,
            Guid currentUserId,
            bool isAdmin,
            CashierShiftQueryDto query,
            CancellationToken cancellationToken = default);

        Task<List<CashierShiftOwnerOptionDto>> GetShiftOwnerOptionsAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default);

        Task<CashierBalanceShiftDto?> GetActiveShiftAsync(
            Guid tenantId,
            Guid cashierId,
            Guid currentUserId,
            bool isAdmin,
            CancellationToken cancellationToken = default);

        Task<CashierBalanceShiftDto?> GetShiftByIdAsync(
            Guid tenantId,
            Guid shiftId,
            Guid currentUserId,
            bool isAdmin,
            CancellationToken cancellationToken = default);

        Task<CashierBalanceShiftDto> OpenShiftAsync(
            Guid tenantId,
            Guid openedByUserId,
            OpenShiftRequest request,
            CancellationToken cancellationToken = default);

        Task<CashierBalanceShiftDto> OpenOwnShiftAsync(
            Guid tenantId,
            Guid currentUserId,
            OpenOwnShiftRequest? request,
            CancellationToken cancellationToken = default);

        Task<CashierBalanceShiftDto> CloseShiftAsync(
            Guid tenantId,
            Guid shiftId,
            Guid currentUserId,
            bool isAdmin,
            CloseShiftRequest request,
            CancellationToken cancellationToken = default);

        Task DeleteShiftAsync(
            Guid tenantId,
            Guid shiftId,
            CancellationToken cancellationToken = default);
    }
}
