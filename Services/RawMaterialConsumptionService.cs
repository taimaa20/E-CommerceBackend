using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services
{
    public sealed class RawMaterialConsumptionService : IRawMaterialConsumptionService
    {
        private readonly IRawMaterialConsumptionRepository _repository;
        private readonly IBranchContext _branchContext;
        private readonly ITenantResolver _tenantResolver;

        public RawMaterialConsumptionService(
            IRawMaterialConsumptionRepository repository,
            IBranchContext branchContext,
            ITenantResolver tenantResolver)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
        }

        public async Task<RawMaterialConsumptionReportDto> GetAsync(
            RawMaterialConsumptionQueryDto query,
            CancellationToken ct)
        {
            Validate(query);
            var branches = (await _branchContext.GetCurrentAsync(ct)).AssignedBranches;
            query.BranchId ??= branches.FirstOrDefault(b => b.IsCurrent)?.Id;
            if (!query.BranchId.HasValue || branches.All(b => b.Id != query.BranchId.Value || !b.IsActive))
                throw new UnauthorizedException("You do not have access to the selected branch.");

            var report = await _repository.GetAsync(query, ct)
                ?? throw new NotFoundException(nameof(Models.RawMaterial), query.RawMaterialId);
            var branch = branches.First(b => b.Id == query.BranchId.Value);
            report.BranchName = branch.Name;
            report.BranchNameAr = branch.NameAr;
            return report;
        }

        public async Task<(RawMaterialConsumptionReportDto Report, IReadOnlyList<RawMaterialConsumptionDetailDto> Details, string CompanyName, string? LogoUrl)> GetExportAsync(
            RawMaterialConsumptionQueryDto query, CancellationToken ct)
        {
            query.ExportAll = true;
            var report = await GetAsync(query, ct);
            var details = await _repository.GetAllDetailsAsync(query, ct);
            var branding = await _repository.GetBrandingAsync(_tenantResolver.GetTenantId(), ct);
            return (report, details, branding.CompanyName, branding.LogoUrl);
        }

        private static void Validate(RawMaterialConsumptionQueryDto query)
        {
            var errors = new Dictionary<string, string[]>();
            if (query.RawMaterialId == Guid.Empty) errors["rawMaterialId"] = ["Raw material is required."];
            if (query.FromUtc == default) errors["fromUtc"] = ["Start date is required."];
            if (query.ToUtc == default) errors["toUtc"] = ["End date is required."];
            if (query.ToUtc <= query.FromUtc) errors["toUtc"] = ["End date must be after start date."];
            if (!Allowed(query.OrderChannel, "dinein", "takeaway", "delivery", "deliverypartner"))
                errors["orderChannel"] = ["Order type is invalid."];
            if (!string.IsNullOrWhiteSpace(query.OrderStatus)
                && !Enum.TryParse<Models.OrderStatus>(query.OrderStatus, true, out _))
                errors["orderStatus"] = ["Order status is invalid."];
            if (!Allowed(query.TimelineGrouping, "hour", "day", "week", "month"))
                errors["timelineGrouping"] = ["Timeline grouping is invalid."];
            if (!Allowed(query.SortBy, "date", "order", "product", "category", "consumed"))
                errors["sortBy"] = ["Sort column is invalid."];
            if (!Allowed(query.SortDirection, "asc", "desc"))
                errors["sortDirection"] = ["Sort direction is invalid."];
            if (errors.Count > 0) throw new ValidationException(errors);
        }

        private static bool Allowed(string? value, params string[] values)
            => string.IsNullOrWhiteSpace(value) || values.Contains(value, StringComparer.OrdinalIgnoreCase);
    }
}
