using RestaurantPos.Api.DTOs.ExpenseInvoice;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services
{
    public sealed class ExpenseCategoryService : IExpenseCategoryService
    {
        private readonly IExpenseCategoryRepository _repo;
        private readonly ITenantResolver _tenantResolver;
        private readonly ILogger<ExpenseCategoryService> _logger;

        public ExpenseCategoryService(
            IExpenseCategoryRepository repo,
            ITenantResolver tenantResolver,
            ILogger<ExpenseCategoryService> logger)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ExpenseCategoryDto> CreateAsync(CreateExpenseCategoryDto dto, bool isArabic, CancellationToken ct)
        {
            var name = (dto.Name ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
                throw new ValidationException("Name is required.");

            if (await _repo.NameExistsAsync(name, excludeId: null, ct))
                throw new ValidationException($"Category '{name}' already exists.");

            var entity = new ExpenseCategory
            {
                Id = Guid.NewGuid(),
                // Stamp TenantId explicitly — the DbContext SaveChanges hook only
                // auto-fills CreatedAt/UpdatedAt. Without this the row is invisible
                // to subsequent reads because the global query filter rejects it.
                TenantId = _tenantResolver.GetTenantId(),
                Name = name,
                NameAr = dto.NameAr?.Trim(),
                Color = dto.Color?.Trim(),
                IsActive = dto.IsActive,
                SortOrder = dto.SortOrder
            };

            await _repo.AddAsync(entity, ct);
            await _repo.SaveChangesAsync(ct);

            _logger.LogInformation("ExpenseCategory {CategoryId} created (name={Name})", entity.Id, entity.Name);
            return ToDto(entity, isArabic, 0);
        }

        public async Task<ExpenseCategoryDto> UpdateAsync(Guid id, UpdateExpenseCategoryDto dto, bool isArabic, CancellationToken ct)
        {
            var entity = await _repo.GetTrackedAsync(id, ct)
                ?? throw new NotFoundException(nameof(ExpenseCategory), id);

            var name = (dto.Name ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
                throw new ValidationException("Name is required.");

            if (!string.Equals(name, entity.Name, StringComparison.Ordinal)
                && await _repo.NameExistsAsync(name, id, ct))
            {
                throw new ValidationException($"Category '{name}' already exists.");
            }

            entity.Name = name;
            entity.NameAr = dto.NameAr?.Trim();
            entity.Color = dto.Color?.Trim();
            entity.IsActive = dto.IsActive;
            entity.SortOrder = dto.SortOrder;

            await _repo.SaveChangesAsync(ct);
            return ToDto(entity, isArabic, 0);
        }

        public async Task SoftDeleteAsync(Guid id, CancellationToken ct)
        {
            var entity = await _repo.GetTrackedAsync(id, ct)
                ?? throw new NotFoundException(nameof(ExpenseCategory), id);

            entity.DeletedAt = DateTime.UtcNow;
            await _repo.SaveChangesAsync(ct);
            _logger.LogInformation("ExpenseCategory {CategoryId} soft-deleted", id);
        }

        public Task<List<ExpenseCategoryDto>> ListAsync(bool isArabic, bool activeOnly, CancellationToken ct)
            => _repo.ListAsync(isArabic, activeOnly, ct);

        private static ExpenseCategoryDto ToDto(ExpenseCategory e, bool isArabic, int invoiceCount) => new()
        {
            Id = e.Id,
            Name = e.Name,
            NameAr = e.NameAr,
            DisplayName = isArabic && !string.IsNullOrEmpty(e.NameAr) ? e.NameAr! : e.Name,
            Color = e.Color,
            IsActive = e.IsActive,
            SortOrder = e.SortOrder,
            InvoiceCount = invoiceCount
        };
    }
}
