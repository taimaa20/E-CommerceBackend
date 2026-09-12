using System.Globalization;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services.Caching;

namespace RestaurantPos.Api.Services
{
    public sealed class CustomerAnalyticsService : ICustomerAnalyticsService
    {
        private static readonly TimeSpan CacheSlidingExpiration = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan CacheAbsoluteExpiration = TimeSpan.FromMinutes(5);
        private readonly ICacheService _cache;
        private readonly ICustomerAnalyticsRepository _repository;
        private readonly ITenantResolver _tenantResolver;
        private readonly IBranchContext _branchContext;
        private readonly ICurrentUserAccessor _currentUser;

        public CustomerAnalyticsService(
            ICustomerAnalyticsRepository repository,
            ITenantResolver tenantResolver,
            ICacheService cache,
            IBranchContext branchContext,
            ICurrentUserAccessor currentUser)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<CustomerAnalyticsSnapshotDto> GetDashboardAsync(
            CustomerAnalyticsQueryDto query,
            bool isArabic,
            CancellationToken ct)
        {
            ValidateDateRange(query.DateFrom, query.DateTo);
            ValidateRange(query.MinOrders, query.MaxOrders, "Minimum orders cannot be greater than maximum orders.");
            ValidateRange(query.MinSpending, query.MaxSpending, "Minimum spending cannot be greater than maximum spending.");

            var branchId = await ResolveBranchScopeAsync(query.BranchId, ct);
            var filter = BuildFilter(query, branchId);
            var cacheKey = BuildCacheKey(filter, isArabic);
            return await _cache.GetOrCreateAsync(
                cacheKey,
                () => _repository.GetDashboardAsync(filter, isArabic, ct),
                CacheSlidingExpiration,
                CacheAbsoluteExpiration,
                ct) ?? new CustomerAnalyticsSnapshotDto();
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

        private CustomerAnalyticsFilter BuildFilter(CustomerAnalyticsQueryDto query, Guid? resolvedBranchId)
            => new()
            {
                TenantId = _tenantResolver.GetTenantId(),
                DateFrom = ToUtcDate(query.DateFrom),
                DateToExclusive = ToUtcDateExclusive(query.DateTo),
                BranchId = resolvedBranchId,
                Name = TrimOrNull(query.Name),
                Phone = TrimOrNull(query.Phone),
                Partner = TrimOrNull(query.Partner),
                CustomerType = query.CustomerType,
                IsActive = query.IsActive,
                MinOrders = query.MinOrders,
                MaxOrders = query.MaxOrders,
                MinSpending = query.MinSpending,
                MaxSpending = query.MaxSpending
            };

        private static void ValidateDateRange(DateTime? dateFrom, DateTime? dateTo)
        {
            if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value.Date > dateTo.Value.Date)
                throw new ValidationException("Date from cannot be after date to.");
        }

        private static void ValidateRange<T>(T? min, T? max, string message)
            where T : struct, IComparable<T>
        {
            if (min.HasValue && max.HasValue && min.Value.CompareTo(max.Value) > 0)
                throw new ValidationException(message);
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

        private static string BuildCacheKey(CustomerAnalyticsFilter filter, bool isArabic)
            => string.Join(":",
                "customer-analytics",
                filter.TenantId,
                isArabic ? "ar" : "en",
                Format(filter.DateFrom),
                Format(filter.DateToExclusive),
                filter.BranchId?.ToString() ?? "-",
                filter.CustomerType?.ToString() ?? "-",
                filter.Partner ?? "-",
                filter.IsActive?.ToString() ?? "-",
                filter.MinOrders?.ToString(CultureInfo.InvariantCulture) ?? "-",
                filter.MaxOrders?.ToString(CultureInfo.InvariantCulture) ?? "-",
                filter.MinSpending?.ToString(CultureInfo.InvariantCulture) ?? "-",
                filter.MaxSpending?.ToString(CultureInfo.InvariantCulture) ?? "-",
                filter.Name ?? "-",
                filter.Phone ?? "-");

        private static string Format(DateTime? value)
            => value?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "-";
    }
}
