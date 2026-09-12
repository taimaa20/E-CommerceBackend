using Microsoft.Extensions.Logging;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Services
{
    public class TableCategoryService : ITableCategoryService
    {
        private const string DuplicateNameMessage = "A category with this name already exists.";

        private readonly ITableCategoryRepository _repository;
        private readonly ITenantResolver _tenantResolver;
        private readonly IBranchContext _branchContext;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly ILogger<TableCategoryService> _logger;

        public TableCategoryService(
            ITableCategoryRepository repository,
            ITenantResolver tenantResolver,
            IBranchContext branchContext,
            ICurrentUserAccessor currentUser,
            ILogger<TableCategoryService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<TableCategoryDto>> GetAllAsync(bool isArabic, CancellationToken cancellationToken = default)
        {
            var branchId = await ResolveBranchIdAsync(cancellationToken);
            return await _repository.GetAllProjectedAsync(isArabic, branchId, cancellationToken);
        }

        public async Task<TableCategoryCreateOutcome> CreateAsync(CreateTableCategoryRequest request, CancellationToken cancellationToken = default)
        {
            var branchId = await ResolveBranchIdAsync(cancellationToken);
            var name = request.Name.Trim();

            if (await _repository.NameExistsAsync(name, branchId, excludeId: null, cancellationToken))
                return new TableCategoryCreateOutcome(null, DuplicateName: true);

            var entity = new TableCategory
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantResolver.GetTenantId(),
                BranchId = branchId,
                Name = name,
                NameAr = string.IsNullOrWhiteSpace(request.NameAr) ? null : request.NameAr.Trim(),
                Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim()
            };

            await _repository.AddAsync(entity, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Table category {CategoryId} ({Name}) created", entity.Id, entity.Name);

            // Newly-created — TableCount is always 0 (matches original behavior).
            var dto = new TableCategoryDto
            {
                Id = entity.Id,
                Name = entity.Name,
                NameAr = entity.NameAr,
                DisplayName = entity.Name,
                Color = entity.Color,
                TableCount = 0
            };

            return new TableCategoryCreateOutcome(dto, DuplicateName: false);
        }

        public async Task<TableCategoryUpdateOutcome> UpdateAsync(Guid id, CreateTableCategoryRequest request, bool isArabic, CancellationToken cancellationToken = default)
        {
            var branchId = await ResolveBranchIdAsync(cancellationToken);
            var entity = await _repository.GetByIdAsync(id, branchId, cancellationToken);
            if (entity == null)
                return new TableCategoryUpdateOutcome(null, NotFound: true, DuplicateName: false);

            var name = request.Name.Trim();

            if (await _repository.NameExistsAsync(name, branchId, excludeId: id, cancellationToken))
                return new TableCategoryUpdateOutcome(null, NotFound: false, DuplicateName: true);

            entity.Name = name;
            entity.NameAr = string.IsNullOrWhiteSpace(request.NameAr) ? null : request.NameAr.Trim();
            entity.Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim();

            await _repository.UpdateAsync(entity, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);

            // Wire-compat: original controller used `category.Tables.Count()` on a
            // freshly-tracked entity where the Tables collection was never loaded,
            // so the value was always 0. Preserved exactly to keep the response
            // byte-identical to pre-refactor behavior.
            var dto = new TableCategoryDto
            {
                Id = entity.Id,
                Name = entity.Name,
                NameAr = entity.NameAr,
                DisplayName = isArabic && !string.IsNullOrEmpty(entity.NameAr) ? entity.NameAr! : entity.Name,
                Color = entity.Color,
                TableCount = 0
            };

            _logger.LogInformation("Table category {CategoryId} updated", entity.Id);

            return new TableCategoryUpdateOutcome(dto, NotFound: false, DuplicateName: false);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var branchId = await ResolveBranchIdAsync(cancellationToken);
            var entity = await _repository.GetByIdAsync(id, branchId, cancellationToken);
            if (entity == null)
                return false;

            await _repository.UnlinkTablesAsync(id, cancellationToken);
            await _repository.RemoveAsync(entity, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Table category {CategoryId} deleted", id);

            return true;
        }

        // No [Authorize] guards GetTableCategories (pre-existing, tracked separately in
        // SECURITY_AUDIT.md H4), so an unauthenticated caller can reach this service.
        // Resolving IBranchContext for such a caller throws, so fall back to the
        // tenant's Main Branch — the same branch every category implicitly belonged
        // to before this module was branch-scoped.
        private async Task<Guid> ResolveBranchIdAsync(CancellationToken cancellationToken)
        {
            if (!_currentUser.UserIdOrNull.HasValue)
            {
                var tenantId = _tenantResolver.GetTenantId();
                var mainBranchId = await _repository.GetMainBranchIdAsync(tenantId, cancellationToken);
                return mainBranchId ?? Guid.Empty;
            }

            var context = await _branchContext.GetCurrentAsync(cancellationToken);
            return context.CurrentBranch.Id;
        }
    }
}
