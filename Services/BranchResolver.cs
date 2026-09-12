using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Services
{
    public class BranchResolver : IBranchResolver
    {
        private readonly IUserBranchRepository _repository;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICurrentUserAccessor _currentUser;

        public BranchResolver(
            IUserBranchRepository repository,
            ITenantResolver tenantResolver,
            ICurrentUserAccessor currentUser)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<BranchContextDto> ResolveAsync(Guid? requestedBranchId, CancellationToken ct = default)
        {
            var userId = _currentUser.UserIdOrNull
                ?? throw new UnauthorizedException("Invalid session.");
            var tenantId = _tenantResolver.GetTenantId();
            var assignments = await _repository.GetAssignmentsAsync(userId, ct);
            if (assignments.Count == 0)
                throw new ValidationException("The current user is not assigned to any branch.");

            var current = ResolveCurrentAssignment(assignments, requestedBranchId);
            return Map(tenantId, userId, current, assignments);
        }

        private static UserBranch ResolveCurrentAssignment(
            IReadOnlyList<UserBranch> assignments,
            Guid? requestedBranchId)
        {
            if (requestedBranchId.HasValue)
            {
                var selected = assignments.FirstOrDefault(a => a.BranchId == requestedBranchId.Value)
                    ?? throw new ForbiddenException("You are not assigned to the selected branch.");
                EnsureActive(selected);
                return selected;
            }

            var defaultAssignment = assignments.FirstOrDefault(a => a.IsDefault);
            if (defaultAssignment is not null)
            {
                EnsureActive(defaultAssignment);
                return defaultAssignment;
            }

            var onlyActive = assignments.Where(a => a.Branch.IsActive).Take(2).ToList();
            if (onlyActive.Count == 1)
                return onlyActive[0];

            throw new ValidationException("Select a default branch before continuing.");
        }

        private static void EnsureActive(UserBranch assignment)
        {
            if (!assignment.Branch.IsActive)
                throw new ValidationException("Inactive branches cannot be used as the current branch.");
        }

        private static BranchContextDto Map(
            Guid tenantId,
            Guid userId,
            UserBranch current,
            IReadOnlyList<UserBranch> assignments)
            => new()
            {
                TenantId = tenantId,
                UserId = userId,
                CurrentBranch = MapBranch(current, current.BranchId),
                AssignedBranches = assignments.Select(a => MapBranch(a, current.BranchId)).ToList(),
                RequiresBranchSelector = assignments.Count > 1
            };

        private static BranchContextBranchDto MapBranch(UserBranch assignment, Guid currentBranchId)
            => new()
            {
                Id = assignment.BranchId,
                Name = assignment.Branch.Name,
                NameAr = assignment.Branch.NameAr,
                Code = assignment.Branch.Code,
                IsMainBranch = assignment.Branch.IsMainBranch,
                IsActive = assignment.Branch.IsActive,
                IsDefault = assignment.IsDefault,
                IsCurrent = assignment.BranchId == currentBranchId
            };
    }
}
