using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Services
{
    public sealed class CustomerManagementService : ICustomerManagementService
    {
        private readonly ICustomerManagementRepository _repository;
        private readonly ITenantResolver _tenantResolver;
        private readonly IBranchContext _branchContext;
        private readonly ICurrentUserAccessor _currentUser;

        public CustomerManagementService(
            ICustomerManagementRepository repository,
            ITenantResolver tenantResolver,
            IBranchContext branchContext,
            ICurrentUserAccessor currentUser)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<PaginatedResponse<CustomerManagementListItemDto>> GetPagedAsync(
            CustomerManagementQueryDto query,
            bool isArabic,
            CancellationToken ct)
        {
            ValidateDateRange(query.DateFrom, query.DateTo);
            var branchId = await ResolveBranchScopeAsync(query.BranchId, ct);
            return await _repository.GetPagedAsync(BuildFilter(query, branchId), isArabic, ct);
        }

        public async Task<CustomerManagementDetailsDto> GetByIdAsync(
            Guid customerId,
            Guid? requestedBranchId,
            bool isArabic,
            CancellationToken ct)
        {
            var branchId = await ResolveBranchScopeAsync(requestedBranchId, ct);
            return await _repository.GetByIdAsync(
                _tenantResolver.GetTenantId(),
                customerId,
                branchId,
                isArabic,
                ct) ?? throw new NotFoundException("Customer", customerId);
        }

        public async Task<PaginatedResponse<CustomerManagementOrderDto>> GetOrdersAsync(
            Guid customerId,
            CustomerOrdersQueryDto query,
            bool isArabic,
            CancellationToken ct)
        {
            ValidateDateRange(query.DateFrom, query.DateTo);
            var branchId = await ResolveBranchScopeAsync(query.BranchId, ct);
            return await _repository.GetOrdersAsync(
                BuildOrdersFilter(customerId, query, branchId),
                isArabic,
                ct) ?? throw new NotFoundException("Customer", customerId);
        }

        // Same scope semantics as ReportService/DashboardFilterBinding: null = current
        // branch, Guid.Empty = all branches (admin/manager only), explicit = must be an
        // assigned active branch. Client-supplied branch ids are never trusted as-is.
        private async Task<Guid?> ResolveBranchScopeAsync(Guid? requestedBranchId, CancellationToken ct)
        {
            if (requestedBranchId == Guid.Empty)
            {
                if (!_currentUser.IsAdminOrManager)
                    throw new ForbiddenException("All branches customer scope requires administrator or manager access.");
                return null;
            }

            var context = await _branchContext.GetCurrentAsync(ct);
            if (!requestedBranchId.HasValue)
                return context.CurrentBranch.Id;

            var selected = context.AssignedBranches.FirstOrDefault(b => b.Id == requestedBranchId.Value);
            if (selected is null)
                throw new ForbiddenException("You are not assigned to the selected branch.");
            if (!selected.IsActive)
                throw new ValidationException("Inactive branches cannot be used as customer scope.");

            return requestedBranchId.Value;
        }

        private CustomerManagementFilter BuildFilter(CustomerManagementQueryDto query, Guid? resolvedBranchId)
            => new()
            {
                TenantId = _tenantResolver.GetTenantId(),
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                Name = TrimOrNull(query.Name),
                Phone = TrimOrNull(query.Phone),
                Partner = TrimOrNull(query.Partner),
                CustomerType = query.CustomerType,
                IsActive = query.IsActive,
                DateFrom = ToUtcDate(query.DateFrom),
                DateToExclusive = ToUtcDateExclusive(query.DateTo),
                BranchId = resolvedBranchId,
                SortBy = query.SortBy,
                SortDirection = query.SortDirection
            };

        private CustomerOrdersFilter BuildOrdersFilter(Guid customerId, CustomerOrdersQueryDto query, Guid? resolvedBranchId)
            => new()
            {
                TenantId = _tenantResolver.GetTenantId(),
                CustomerId = customerId,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                Search = TrimOrNull(query.Search),
                Status = query.Status,
                DateFrom = ToUtcDate(query.DateFrom),
                DateToExclusive = ToUtcDateExclusive(query.DateTo),
                BranchId = resolvedBranchId,
                SortBy = query.SortBy,
                SortDirection = query.SortDirection
            };

        private static void ValidateDateRange(DateTime? dateFrom, DateTime? dateTo)
        {
            if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value.Date > dateTo.Value.Date)
                throw new ValidationException("Date from cannot be after date to.");
        }

        private static DateTime? ToUtcDate(DateTime? value)
            => value.HasValue
                ? DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc)
                : null;

        private static DateTime? ToUtcDateExclusive(DateTime? value)
            => value.HasValue
                ? DateTime.SpecifyKind(value.Value.Date.AddDays(1), DateTimeKind.Utc)
                : null;

        private static string? TrimOrNull(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
