using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Services
{
    public class BranchService : IBranchService
    {
        private readonly IBranchRepository _repository;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IConfigAuditService _audit;
        private readonly ILogger<BranchService> _logger;

        private Guid TenantId => _tenantResolver.GetTenantId();

        public BranchService(
            IBranchRepository repository,
            ITenantResolver tenantResolver,
            ICurrentUserAccessor currentUser,
            IConfigAuditService audit,
            ILogger<BranchService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<BranchPagedResultDto> GetPagedAsync(
            string? search,
            bool? isActive,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 200 ? 25 : pageSize;

            var (items, totalCount) = await _repository.GetPagedAsync(search, isActive, page, pageSize, ct);
            return new BranchPagedResultDto
            {
                Items = items.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<BranchDto> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var branch = await _repository.GetByIdAsync(id, ct)
                ?? throw new NotFoundException(nameof(Branch), id);
            return MapToDto(branch);
        }

        public async Task<BranchDto> CreateAsync(BranchCreateDto dto, CancellationToken ct = default)
        {
            var normalized = Normalize(dto);
            var isFirstBranch = !await _repository.AnyAsync(ct);
            var makeMain = isFirstBranch || dto.IsMainBranch;
            await ValidateForCreateAsync(normalized, makeMain, ct);

            var branch = CreateEntity(normalized, makeMain);
            var created = await _repository.AddAsync(branch, clearExistingMain: makeMain, _currentUser.UserIdOrNull, ct);
            await AuditAsync(null, created, "BranchCreated", ct);

            _logger.LogInformation("Branch {BranchId} created with code {BranchCode}", created.Id, created.Code);
            return MapToDto(created);
        }

        public async Task<BranchDto> UpdateAsync(Guid id, BranchUpdateDto dto, CancellationToken ct = default)
        {
            var branch = await _repository.GetTrackedAsync(id, ct)
                ?? throw new NotFoundException(nameof(Branch), id);
            var previous = Snapshot(branch);
            var normalized = Normalize(dto);
            await ValidateForUpdateAsync(branch, normalized, ct);

            var makeMain = !branch.IsMainBranch && normalized.IsMainBranch;
            Apply(normalized, branch);
            var updated = await _repository.UpdateAsync(branch, clearExistingMain: makeMain, _currentUser.UserIdOrNull, ct);
            await AuditAsync(previous, updated, "BranchUpdated", ct);

            _logger.LogInformation("Branch {BranchId} updated", updated.Id);
            return MapToDto(updated);
        }

        public async Task<BranchDto> ActivateAsync(Guid id, CancellationToken ct = default)
        {
            var branch = await _repository.GetTrackedAsync(id, ct)
                ?? throw new NotFoundException(nameof(Branch), id);
            if (branch.IsActive) return MapToDto(branch);

            var previous = Snapshot(branch);
            branch.IsActive = true;
            StampUpdater(branch);
            var updated = await _repository.UpdateAsync(branch, clearExistingMain: false, _currentUser.UserIdOrNull, ct);
            await AuditAsync(previous, updated, "BranchActivated", ct);
            return MapToDto(updated);
        }

        public async Task<BranchDto> DeactivateAsync(Guid id, CancellationToken ct = default)
        {
            var branch = await _repository.GetTrackedAsync(id, ct)
                ?? throw new NotFoundException(nameof(Branch), id);
            if (branch.IsMainBranch)
                throw new ValidationException("The Main Branch cannot be deactivated.");
            if (!branch.IsActive) return MapToDto(branch);

            var previous = Snapshot(branch);
            branch.IsActive = false;
            StampUpdater(branch);
            var updated = await _repository.UpdateAsync(branch, clearExistingMain: false, _currentUser.UserIdOrNull, ct);
            await AuditAsync(previous, updated, "BranchDeactivated", ct);
            return MapToDto(updated);
        }

        public async Task<BranchDto> SetMainAsync(Guid id, CancellationToken ct = default)
        {
            var branch = await _repository.GetTrackedAsync(id, ct)
                ?? throw new NotFoundException(nameof(Branch), id);
            if (!branch.IsActive)
                throw new ValidationException("Inactive branches cannot become Main Branch.");
            if (branch.IsMainBranch) return MapToDto(branch);

            var previousMain = await _repository.GetMainAsync(ct);
            var previous = MainBranchTransition(previousMain, branch);
            branch.IsMainBranch = true;
            StampUpdater(branch);
            var updated = await _repository.UpdateAsync(branch, clearExistingMain: true, _currentUser.UserIdOrNull, ct);
            var next = MainBranchTransition(previousMain, updated, previousMainIsMain: false);
            await AuditAsync(previous, next, updated.Id, updated.Code, "MainBranchChanged", ct);
            return MapToDto(updated);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var branch = await _repository.GetTrackedAsync(id, ct)
                ?? throw new NotFoundException(nameof(Branch), id);
            if (branch.IsMainBranch)
                throw new ValidationException("The Main Branch cannot be deleted.");

            var previous = Snapshot(branch);
            await _repository.DeleteAsync(branch, ct);
            await AuditAsync(previous, null, "BranchDeleted", ct);
            _logger.LogInformation("Branch {BranchId} soft-deleted", id);
        }

        private async Task ValidateForCreateAsync(BranchDraft draft, bool makeMain, CancellationToken ct)
        {
            ValidateRequired(draft, makeMain);
            if (await _repository.CodeExistsAsync(draft.Code, null, ct))
                throw FieldValidation("code", $"Branch code '{draft.Code}' already exists.");
            if (await _repository.NameExistsAsync(draft.Name, null, ct))
                throw FieldValidation("name", $"Branch name '{draft.Name}' already exists.");
        }

        private async Task ValidateForUpdateAsync(Branch branch, BranchDraft draft, CancellationToken ct)
        {
            ValidateRequired(draft, draft.IsMainBranch);
            if (branch.IsMainBranch && !draft.IsMainBranch)
                throw new ValidationException("Use Set Main Branch to move the Main Branch flag to another branch.");
            if (branch.IsMainBranch && !draft.IsActive)
                throw new ValidationException("The Main Branch cannot be deactivated.");
            if (await _repository.CodeExistsAsync(draft.Code, branch.Id, ct))
                throw FieldValidation("code", $"Branch code '{draft.Code}' already exists.");
            if (await _repository.NameExistsAsync(draft.Name, branch.Id, ct))
                throw FieldValidation("name", $"Branch name '{draft.Name}' already exists.");
        }

        private static void ValidateRequired(BranchDraft draft, bool makeMain)
        {
            if (string.IsNullOrWhiteSpace(draft.Name))
                throw FieldValidation("name", "Branch name is required.");
            if (string.IsNullOrWhiteSpace(draft.Code))
                throw FieldValidation("code", "Branch code is required.");
            if (makeMain && !draft.IsActive)
                throw new ValidationException("Inactive branches cannot become Main Branch.");
        }

        private Branch CreateEntity(BranchDraft draft, bool makeMain)
        {
            var branch = new Branch
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                CreatedById = _currentUser.UserIdOrNull,
                UpdatedById = _currentUser.UserIdOrNull
            };
            Apply(draft with { IsMainBranch = makeMain }, branch);
            branch.CreatedById = _currentUser.UserIdOrNull;
            return branch;
        }

        private void Apply(BranchDraft draft, Branch branch)
        {
            branch.Name = draft.Name;
            branch.NameAr = draft.NameAr;
            branch.Code = draft.Code;
            branch.Description = draft.Description;
            branch.Address = draft.Address;
            branch.Phone = draft.Phone;
            branch.Email = draft.Email;
            branch.IsActive = draft.IsActive;
            branch.IsMainBranch = draft.IsMainBranch;
            StampUpdater(branch);
        }

        private void StampUpdater(Branch branch)
        {
            branch.UpdatedById = _currentUser.UserIdOrNull;
        }

        private Task AuditAsync(object? previous, Branch? next, string reason, CancellationToken ct)
            => _audit.LogAsync(
                TenantId,
                ConfigAuditEventType.BranchConfigChanged,
                previous,
                next is null ? null : Snapshot(next),
                targetId: next?.Id ?? (previous as BranchSnapshot)?.Id,
                branchCode: next?.Code ?? (previous as BranchSnapshot)?.Code,
                reason: reason,
                ct: ct);

        private Task AuditAsync(
            object? previous,
            object? next,
            Guid targetId,
            string branchCode,
            string reason,
            CancellationToken ct)
            => _audit.LogAsync(
                TenantId,
                ConfigAuditEventType.BranchConfigChanged,
                previous,
                next,
                targetId: targetId,
                branchCode: branchCode,
                reason: reason,
                ct: ct);

        private static MainBranchTransitionSnapshot MainBranchTransition(
            Branch? previousMain,
            Branch nextMain,
            bool? previousMainIsMain = null)
            => new(
                previousMain is null ? null : Snapshot(previousMain, previousMainIsMain),
                Snapshot(nextMain));

        private static BranchSnapshot Snapshot(Branch branch, bool? isMainBranch = null) => new(
            branch.Id,
            branch.Name,
            branch.NameAr,
            branch.Code,
            branch.Description,
            branch.Address,
            branch.Phone,
            branch.Email,
            isMainBranch ?? branch.IsMainBranch,
            branch.IsActive);

        private static BranchDto MapToDto(Branch branch) => new()
        {
            Id = branch.Id,
            TenantId = branch.TenantId,
            Name = branch.Name,
            NameAr = branch.NameAr,
            Code = branch.Code,
            Description = branch.Description,
            Address = branch.Address,
            Phone = branch.Phone,
            Email = branch.Email,
            IsMainBranch = branch.IsMainBranch,
            IsActive = branch.IsActive,
            CreatedById = branch.CreatedById,
            CreatedBy = branch.CreatedBy,
            UpdatedById = branch.UpdatedById,
            UpdatedBy = branch.UpdatedBy,
            CreatedAt = branch.CreatedAt,
            UpdatedAt = branch.UpdatedAt
        };

        private static BranchDraft Normalize(BranchCreateDto dto) => new(
            NormalizeRequired(dto.Name),
            NormalizeOptional(dto.NameAr),
            NormalizeCode(dto.Code),
            NormalizeOptional(dto.Description),
            NormalizeOptional(dto.Address),
            NormalizeOptional(dto.Phone),
            NormalizeOptional(dto.Email),
            dto.IsMainBranch,
            dto.IsActive);

        private static BranchDraft Normalize(BranchUpdateDto dto) => new(
            NormalizeRequired(dto.Name),
            NormalizeOptional(dto.NameAr),
            NormalizeCode(dto.Code),
            NormalizeOptional(dto.Description),
            NormalizeOptional(dto.Address),
            NormalizeOptional(dto.Phone),
            NormalizeOptional(dto.Email),
            dto.IsMainBranch,
            dto.IsActive);

        private static string NormalizeRequired(string? value) => (value ?? string.Empty).Trim();

        private static string NormalizeCode(string? value)
            => (value ?? string.Empty).Trim().ToUpperInvariant().Replace(' ', '_');

        private static string? NormalizeOptional(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static ValidationException FieldValidation(string field, string message)
            => new(new Dictionary<string, string[]> { [field] = new[] { message } });

        private sealed record BranchDraft(
            string Name,
            string? NameAr,
            string Code,
            string? Description,
            string? Address,
            string? Phone,
            string? Email,
            bool IsMainBranch,
            bool IsActive);

        private sealed record BranchSnapshot(
            Guid Id,
            string Name,
            string? NameAr,
            string Code,
            string? Description,
            string? Address,
            string? Phone,
            string? Email,
            bool IsMainBranch,
            bool IsActive);

        private sealed record MainBranchTransitionSnapshot(
            BranchSnapshot? OldMainBranch,
            BranchSnapshot NewMainBranch);
    }
}
