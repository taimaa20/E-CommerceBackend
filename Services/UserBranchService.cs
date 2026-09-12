using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Services
{
    public class UserBranchService : IUserBranchService
    {
        private readonly IUserBranchRepository _repository;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IConfigAuditService _audit;

        private Guid TenantId => _tenantResolver.GetTenantId();

        public UserBranchService(
            IUserBranchRepository repository,
            ITenantResolver tenantResolver,
            ICurrentUserAccessor currentUser,
            IConfigAuditService audit)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        }

        public async Task<UserBranchAssignmentsDto> GetAssignmentsAsync(Guid userId, CancellationToken ct = default)
        {
            var user = await GetUserAsync(userId, ct);
            var assignments = await _repository.GetAssignmentsAsync(userId, ct);
            return Map(user, assignments);
        }

        public async Task<UserBranchAssignmentsDto> AssignAsync(
            Guid userId,
            UserBranchAssignDto dto,
            CancellationToken ct = default)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var user = await GetUserAsync(userId, ct);
            var branch = await GetActiveBranchAsync(dto.BranchId, ct);
            if (await _repository.GetAssignmentAsync(userId, dto.BranchId, ct) is not null)
                throw FieldValidation("branchId", "This branch is already assigned to the user.");

            var isFirstAssignment = await _repository.CountAssignmentsAsync(userId, ct) == 0;
            var existingDefault = isFirstAssignment ? null : await _repository.GetDefaultAsync(userId, ct);
            var makeDefault = isFirstAssignment || dto.IsDefault || existingDefault is null;
            var previousDefault = makeDefault && !isFirstAssignment
                ? existingDefault
                : null;
            var assignment = new UserBranch
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                UserId = userId,
                BranchId = dto.BranchId,
                IsDefault = makeDefault,
                CreatedById = _currentUser.UserIdOrNull,
                UpdatedById = _currentUser.UserIdOrNull
            };

            await _repository.AddAsync(assignment, clearExistingDefault: makeDefault, _currentUser.UserIdOrNull, ct);
            await AuditAsync(null, Snapshot(user, branch, makeDefault), userId, branch.Code, "UserBranchAssigned", ct);

            if (makeDefault)
            {
                await AuditDefaultChangeAsync(user, previousDefault, assignment, branch, ct);
            }

            return await GetAssignmentsAsync(userId, ct);
        }

        public async Task<UserBranchAssignmentsDto> RemoveAsync(
            Guid userId,
            Guid branchId,
            UserBranchRemoveDto dto,
            CancellationToken ct = default)
        {
            dto ??= new UserBranchRemoveDto();
            var user = await GetUserAsync(userId, ct);
            var assignment = await _repository.GetTrackedAssignmentAsync(userId, branchId, ct)
                ?? throw new NotFoundException(nameof(UserBranch), branchId);

            var count = await _repository.CountAssignmentsAsync(userId, ct);
            if (count <= 1)
                throw new ValidationException("A user must always have at least one assigned branch.");

            UserBranch? newDefault = null;
            Branch? newDefaultBranch = null;
            var previousDefault = assignment.IsDefault ? Snapshot(user, assignment) : null;

            if (assignment.IsDefault)
            {
                if (!dto.NewDefaultBranchId.HasValue || dto.NewDefaultBranchId.Value == Guid.Empty)
                    throw FieldValidation("newDefaultBranchId", "Select another default branch before removing the current default branch.");
                if (dto.NewDefaultBranchId.Value == branchId)
                    throw FieldValidation("newDefaultBranchId", "The removed branch cannot also be the new default branch.");

                newDefault = await _repository.GetTrackedAssignmentAsync(userId, dto.NewDefaultBranchId.Value, ct)
                    ?? throw FieldValidation("newDefaultBranchId", "The new default branch must already be assigned to the user.");
                newDefaultBranch = newDefault.Branch;
                if (!newDefaultBranch.IsActive)
                    throw FieldValidation("newDefaultBranchId", "Inactive branches cannot become default.");
            }

            var removed = Snapshot(user, assignment);
            await _repository.RemoveAsync(assignment, newDefault, _currentUser.UserIdOrNull, ct);
            await AuditAsync(removed, null, userId, assignment.Branch.Code, "UserBranchRemoved", ct);

            if (previousDefault is not null && newDefault is not null && newDefaultBranch is not null)
                await AuditAsync(previousDefault, Snapshot(user, newDefault, newDefaultBranch), userId, newDefaultBranch.Code, "DefaultBranchChanged", ct);

            return await GetAssignmentsAsync(userId, ct);
        }

        public async Task<UserBranchAssignmentsDto> SetDefaultAsync(Guid userId, Guid branchId, CancellationToken ct = default)
        {
            var user = await GetUserAsync(userId, ct);
            var assignment = await _repository.GetTrackedAssignmentAsync(userId, branchId, ct)
                ?? throw new NotFoundException(nameof(UserBranch), branchId);
            if (!assignment.Branch.IsActive)
                throw new ValidationException("Inactive branches cannot become default.");
            if (assignment.IsDefault)
                return await GetAssignmentsAsync(userId, ct);

            var previousDefault = await _repository.GetDefaultAsync(userId, ct);
            await _repository.SetDefaultAsync(assignment, _currentUser.UserIdOrNull, ct);
            await AuditDefaultChangeAsync(user, previousDefault, assignment, assignment.Branch, ct);

            return await GetAssignmentsAsync(userId, ct);
        }

        private async Task<UserBranchUserSummary> GetUserAsync(Guid userId, CancellationToken ct)
            => await _repository.GetUserSummaryAsync(userId, ct)
                ?? throw new NotFoundException(nameof(User), userId);

        private async Task<Branch> GetActiveBranchAsync(Guid branchId, CancellationToken ct)
        {
            var branch = await _repository.GetBranchAsync(branchId, ct)
                ?? throw new NotFoundException(nameof(Branch), branchId);
            if (!branch.IsActive)
                throw FieldValidation("branchId", "Inactive branches cannot be assigned.");
            return branch;
        }

        private Task AuditDefaultChangeAsync(
            UserBranchUserSummary user,
            UserBranch? previousDefault,
            UserBranch nextDefault,
            Branch nextBranch,
            CancellationToken ct)
            => AuditAsync(
                previousDefault is null ? null : Snapshot(user, previousDefault),
                Snapshot(user, nextDefault, nextBranch),
                user.Id,
                nextBranch.Code,
                "DefaultBranchChanged",
                ct);

        private Task AuditAsync(
            object? previous,
            object? next,
            Guid targetId,
            string branchCode,
            string reason,
            CancellationToken ct)
            => _audit.LogAsync(
                TenantId,
                ConfigAuditEventType.UserBranchAssignmentChanged,
                previous,
                next,
                targetId: targetId,
                branchCode: branchCode,
                reason: reason,
                ct: ct);

        private static UserBranchAssignmentsDto Map(
            UserBranchUserSummary user,
            IReadOnlyList<UserBranch> assignments)
            => new()
            {
                UserId = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                FullNameAr = user.FullNameAr,
                Assignments = assignments.Select(MapAssignment).ToList()
            };

        private static UserBranchAssignmentDto MapAssignment(UserBranch assignment)
            => new()
            {
                Id = assignment.Id,
                UserId = assignment.UserId,
                BranchId = assignment.BranchId,
                BranchName = assignment.Branch.Name,
                BranchNameAr = assignment.Branch.NameAr,
                BranchCode = assignment.Branch.Code,
                BranchIsActive = assignment.Branch.IsActive,
                IsDefault = assignment.IsDefault,
                CreatedAt = assignment.CreatedAt,
                UpdatedAt = assignment.UpdatedAt
            };

        private static UserBranchAssignmentSnapshot Snapshot(
            UserBranchUserSummary user,
            UserBranch assignment)
            => Snapshot(user, assignment, assignment.Branch);

        private static UserBranchAssignmentSnapshot Snapshot(
            UserBranchUserSummary user,
            UserBranch assignment,
            Branch branch)
            => new(
                user.Id,
                user.Username,
                branch.Id,
                branch.Code,
                branch.Name,
                branch.NameAr,
                branch.IsActive,
                assignment.IsDefault);

        private static UserBranchAssignmentSnapshot Snapshot(
            UserBranchUserSummary user,
            Branch branch,
            bool isDefault)
            => new(
                user.Id,
                user.Username,
                branch.Id,
                branch.Code,
                branch.Name,
                branch.NameAr,
                branch.IsActive,
                isDefault);

        private static ValidationException FieldValidation(string field, string message)
            => new(new Dictionary<string, string[]> { [field] = new[] { message } });

        private sealed record UserBranchAssignmentSnapshot(
            Guid UserId,
            string Username,
            Guid BranchId,
            string BranchCode,
            string BranchName,
            string? BranchNameAr,
            bool BranchIsActive,
            bool IsDefault);
    }
}
