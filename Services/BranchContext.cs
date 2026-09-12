using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Services
{
    public class BranchContext : IBranchContext
    {
        private readonly ICurrentBranchProvider _currentBranchProvider;
        private readonly IBranchResolver _resolver;
        private readonly IConfigAuditService _audit;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICurrentUserAccessor _currentUser;
        private BranchContextDto? _current;

        public BranchContext(
            ICurrentBranchProvider currentBranchProvider,
            IBranchResolver resolver,
            IConfigAuditService audit,
            ITenantResolver tenantResolver,
            ICurrentUserAccessor currentUser)
        {
            _currentBranchProvider = currentBranchProvider ?? throw new ArgumentNullException(nameof(currentBranchProvider));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<BranchContextDto> GetCurrentAsync(CancellationToken ct = default)
        {
            if (_current is not null)
                return _current;

            _current = await _resolver.ResolveAsync(_currentBranchProvider.GetSelectedBranchId(), ct);
            return _current;
        }

        public async Task<BranchContextDto> SwitchAsync(Guid branchId, CancellationToken ct = default)
        {
            if (branchId == Guid.Empty)
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["branchId"] = new[] { "Branch is required." }
                });

            var previous = await GetCurrentAsync(ct);
            var next = await _resolver.ResolveAsync(branchId, ct);

            if (previous.CurrentBranch.Id != next.CurrentBranch.Id)
            {
                await _audit.LogAsync(
                    _tenantResolver.GetTenantId(),
                    ConfigAuditEventType.BranchContextChanged,
                    Snapshot(previous.CurrentBranch),
                    Snapshot(next.CurrentBranch),
                    targetId: _currentUser.UserIdOrNull,
                    branchCode: next.CurrentBranch.Code,
                    reason: "BranchSwitched",
                    ct: ct);
            }

            _current = next;
            return next;
        }

        private static object Snapshot(BranchContextBranchDto branch)
            => new
            {
                branch.Id,
                branch.Code,
                branch.Name,
                branch.NameAr,
                branch.IsMainBranch,
                branch.IsActive
            };
    }
}
