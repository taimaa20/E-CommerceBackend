using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Modules.CustomerMobile.Repositories;

namespace RestaurantPos.Api.Modules.CustomerMobile.Services
{
    public class CustomerMobileBranchService : ICustomerMobileBranchService
    {
        private const int DefaultPageSize = 25;
        private const int MaximumPageSize = 100;

        private readonly ICustomerMobileBranchRepository _repository;
        private readonly ICustomerMobileContext _mobileContext;

        public CustomerMobileBranchService(
            ICustomerMobileBranchRepository repository,
            ICustomerMobileContext mobileContext)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _mobileContext = mobileContext ?? throw new ArgumentNullException(nameof(mobileContext));
        }

        public async Task<PaginatedResponse<CustomerMobileBranchDto>> GetActiveAsync(
            int page,
            int pageSize,
            CancellationToken ct)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > MaximumPageSize ? DefaultPageSize : pageSize;
            var tenantId = _mobileContext.GetTenantId();
            var result = await _repository.GetActiveAsync(tenantId, page, pageSize, ct);
            return new PaginatedResponse<CustomerMobileBranchDto>
            {
                Items = result.Items.Select(MapBranch).ToList(),
                TotalCount = result.TotalCount,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        public async Task<List<PaymentMethodDto>> GetPaymentMethodsAsync(Guid branchId, CancellationToken ct)
        {
            var tenantId = _mobileContext.GetTenantId();
            if (branchId == Guid.Empty || !await _repository.IsActiveAsync(tenantId, branchId, ct))
                throw new ValidationException("The selected branch is invalid or inactive.");

            var methods = await _repository.GetPaymentMethodsAsync(tenantId, branchId, ct);
            return methods.Select(MapPaymentMethod).ToList();
        }

        private static CustomerMobileBranchDto MapBranch(Branch branch) => new()
        {
            Id = branch.Id,
            Name = branch.Name,
            NameAr = branch.NameAr,
            Code = branch.Code,
            Address = branch.Address,
            Phone = branch.Phone,
            IsMainBranch = branch.IsMainBranch
        };

        private static PaymentMethodDto MapPaymentMethod(PaymentMethod method) => new()
        {
            Id = method.Id,
            NameEn = method.NameEn,
            NameAr = method.NameAr,
            Code = method.Code,
            Icon = method.Icon,
            DisplayOrder = method.DisplayOrder,
            IsActive = method.IsActive,
            IsDefault = method.IsDefault,
            RequiresReferenceNumber = method.RequiresReferenceNumber,
            CostSharingMode = method.CostSharingMode,
            CostSharingScope = method.CostSharingScope,
            CostSharingCommissionPercentage = method.CostSharingCommissionPercentage,
            CostSharingRestaurantPercentage = method.CostSharingRestaurantPercentage,
            CostSharingCounterpartyPercentage = method.CostSharingCounterpartyPercentage,
            CreatedAt = method.CreatedAt,
            UpdatedAt = method.UpdatedAt
        };
    }
}
