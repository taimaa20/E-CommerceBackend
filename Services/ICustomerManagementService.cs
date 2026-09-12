using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public interface ICustomerManagementService
    {
        Task<PaginatedResponse<CustomerManagementListItemDto>> GetPagedAsync(
            CustomerManagementQueryDto query,
            bool isArabic,
            CancellationToken ct);

        Task<CustomerManagementDetailsDto> GetByIdAsync(
            Guid customerId,
            Guid? requestedBranchId,
            bool isArabic,
            CancellationToken ct);

        Task<PaginatedResponse<CustomerManagementOrderDto>> GetOrdersAsync(
            Guid customerId,
            CustomerOrdersQueryDto query,
            bool isArabic,
            CancellationToken ct);
    }
}
