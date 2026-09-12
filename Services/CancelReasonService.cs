using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services.Caching;

namespace RestaurantPos.Api.Services
{
    public class CancelReasonService : ICancelReasonService
    {
        private static readonly TimeSpan CacheSliding  = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan CacheAbsolute = TimeSpan.FromHours(1);

        private readonly PosDbContext _context;
        private readonly ICacheService _cache;

        public CancelReasonService(PosDbContext context, ICacheService cache)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cache   = cache   ?? throw new ArgumentNullException(nameof(cache));
        }

        public async Task<IReadOnlyList<CancelReasonDto>> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            var cacheKey = CacheKey(tenantId);

            var cached = await _cache.GetOrCreateAsync(
                cacheKey,
                async () => await LoadAsync(tenantId, cancellationToken),
                slidingExpiration: CacheSliding,
                absoluteExpiration: CacheAbsolute,
                cancellationToken: cancellationToken);

            return (IReadOnlyList<CancelReasonDto>?)cached ?? Array.Empty<CancelReasonDto>();
        }

        public async Task<CancelReasonDetailDto> CreateAsync(
            Guid tenantId,
            CancelReasonUpsertRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateUpsert(request);

            var code = request.Code.Trim().ToUpperInvariant();

            var exists = await _context.CancelReasons
                .AsNoTracking()
                .AnyAsync(r => r.Code == code && (r.TenantId == null || r.TenantId == tenantId), cancellationToken);
            if (exists) throw new ConflictException("A cancel reason with this code already exists.");

            var nextSortOrder = await _context.CancelReasons
                .AsNoTracking()
                .Where(r => r.TenantId == tenantId || r.TenantId == null)
                .Select(r => (int?)r.SortOrder)
                .MaxAsync(cancellationToken) ?? 0;

            var reason = new CancelReason
            {
                Code = code,
                Name = request.Name.Trim(),
                NameAr = request.NameAr.Trim(),
                RequiresNote = request.RequiresNote,
                IsActive = request.IsActive,
                SortOrder = request.SortOrder ?? (nextSortOrder + 1),
                TenantId = tenantId
            };

            _context.CancelReasons.Add(reason);
            await _context.SaveChangesAsync(cancellationToken);
            await _cache.RemoveAsync(CacheKey(tenantId), cancellationToken);

            return Map(reason);
        }

        public async Task<CancelReasonDetailDto> UpdateAsync(
            Guid tenantId,
            int id,
            CancelReasonUpsertRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            // Only the tenant's own rows are editable. Global rows (TenantId == null)
            // are read-only from a tenant context, preventing the previous "claim attack"
            // where editing a global row flipped its TenantId to the editor.
            var reason = await _context.CancelReasons
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, cancellationToken)
                ?? throw new NotFoundException("Cancel reason not found.");

            ValidateUpsert(request);

            var code = request.Code.Trim().ToUpperInvariant();
            var duplicate = await _context.CancelReasons
                .AsNoTracking()
                .AnyAsync(
                    r => r.Id != id && r.Code == code && (r.TenantId == tenantId || r.TenantId == null),
                    cancellationToken);
            if (duplicate) throw new ConflictException("A cancel reason with this code already exists.");

            reason.Code = code;
            reason.Name = request.Name.Trim();
            reason.NameAr = request.NameAr.Trim();
            reason.RequiresNote = request.RequiresNote;
            reason.IsActive = request.IsActive;
            if (request.SortOrder.HasValue) reason.SortOrder = request.SortOrder.Value;

            await _context.SaveChangesAsync(cancellationToken);
            await _cache.RemoveAsync(CacheKey(tenantId), cancellationToken);

            return Map(reason);
        }

        private async Task<List<CancelReasonDto>> LoadAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            return await _context.CancelReasons
                .AsNoTracking()
                .Where(r => r.IsActive && (r.TenantId == null || r.TenantId == tenantId))
                .OrderBy(r => r.SortOrder)
                .Select(r => new CancelReasonDto
                {
                    Id = r.Id,
                    Code = r.Code,
                    Name = r.Name,
                    NameAr = r.NameAr,
                    RequiresNote = r.RequiresNote
                })
                .ToListAsync(cancellationToken);
        }

        private static void ValidateUpsert(CancelReasonUpsertRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Code)
                || string.IsNullOrWhiteSpace(request.Name)
                || string.IsNullOrWhiteSpace(request.NameAr))
            {
                throw new ValidationException("Code, English name, and Arabic name are required.");
            }
        }

        private static CancelReasonDetailDto Map(CancelReason r) => new()
        {
            Id = r.Id,
            Code = r.Code,
            Name = r.Name,
            NameAr = r.NameAr,
            RequiresNote = r.RequiresNote,
            IsActive = r.IsActive,
            SortOrder = r.SortOrder
        };

        private static string CacheKey(Guid tenantId) => $"cancel-reasons:{tenantId}";
    }
}
