using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public class DiscountGroupService : IDiscountGroupService
    {
        private readonly PosDbContext _context;
        private readonly ILogger<DiscountGroupService> _logger;

        public DiscountGroupService(PosDbContext context, ILogger<DiscountGroupService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<DiscountGroupDto>> GetAllAsync(CancellationToken ct = default)
        {
            // Tenant + soft-delete scoping is enforced by the global query filter.
            return await _context.DiscountGroups
                .AsNoTracking()
                .OrderByDescending(g => g.CreatedAt)
                .Select(g => MapProjection(g))
                .ToListAsync(ct);
        }

        public async Task<List<DiscountGroupOptionDto>> GetActiveOptionsAsync(CancellationToken ct = default)
        {
            return await _context.DiscountGroups
                .AsNoTracking()
                .Where(g => g.IsActive)
                .OrderBy(g => g.Name)
                .Select(g => new DiscountGroupOptionDto
                {
                    Id = g.Id,
                    Name = g.Name,
                    NameAr = g.NameAr,
                    DiscountType = g.DiscountType,
                    DiscountValue = g.DiscountValue,
                    VerificationNote = g.VerificationNote
                })
                .ToListAsync(ct);
        }

        public async Task<DiscountGroupDto> CreateAsync(Guid tenantId, DiscountGroupCreateDto input, CancellationToken ct = default)
        {
            var group = new DiscountGroup
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = input.Name.Trim(),
                NameAr = string.IsNullOrWhiteSpace(input.NameAr) ? null : input.NameAr.Trim(),
                DiscountType = input.DiscountType,
                DiscountValue = NormalizeValue(input.DiscountType, input.DiscountValue),
                IsActive = input.IsActive,
                Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
                VerificationNote = string.IsNullOrWhiteSpace(input.VerificationNote) ? null : input.VerificationNote.Trim()
            };

            _context.DiscountGroups.Add(group);
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Discount group {GroupId} created: {Name} {Type} {Value}", group.Id, group.Name, group.DiscountType, group.DiscountValue);
            return MapDto(group);
        }

        public async Task<DiscountGroupDto?> UpdateAsync(Guid id, DiscountGroupUpdateDto input, CancellationToken ct = default)
        {
            var group = await _context.DiscountGroups.FirstOrDefaultAsync(g => g.Id == id, ct);
            if (group == null)
                return null;

            group.Name = input.Name.Trim();
            group.NameAr = string.IsNullOrWhiteSpace(input.NameAr) ? null : input.NameAr.Trim();
            group.DiscountType = input.DiscountType;
            group.DiscountValue = NormalizeValue(input.DiscountType, input.DiscountValue);
            group.IsActive = input.IsActive;
            group.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
            group.VerificationNote = string.IsNullOrWhiteSpace(input.VerificationNote) ? null : input.VerificationNote.Trim();

            await _context.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Discount group {GroupId} updated: {Name} {Type} {Value} active={Active}",
                group.Id, group.Name, group.DiscountType, group.DiscountValue, group.IsActive);
            return MapDto(group);
        }

        // A percentage is bounded 0–100; a flat amount only needs to be non-negative.
        private static decimal NormalizeValue(DiscountValueType type, decimal value)
            => type == DiscountValueType.FixedAmount
                ? Math.Max(0m, value)
                : Math.Clamp(value, 0m, 100m);

        public async Task<DiscountGroupDto?> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
        {
            var group = await _context.DiscountGroups.FirstOrDefaultAsync(g => g.Id == id, ct);
            if (group == null)
                return null;

            group.IsActive = isActive;
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Discount group {GroupId} active state set to {Active}", group.Id, isActive);
            return MapDto(group);
        }

        // Expression form for EF projection (GetAll).
        private static DiscountGroupDto MapProjection(DiscountGroup g) => new()
        {
            Id = g.Id,
            Name = g.Name,
            NameAr = g.NameAr,
            DiscountType = g.DiscountType,
            DiscountValue = g.DiscountValue,
            IsActive = g.IsActive,
            Description = g.Description,
            VerificationNote = g.VerificationNote,
            CreatedAt = g.CreatedAt,
            UpdatedAt = g.UpdatedAt
        };

        private static DiscountGroupDto MapDto(DiscountGroup g) => MapProjection(g);
    }
}
