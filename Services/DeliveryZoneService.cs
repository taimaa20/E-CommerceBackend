using RestaurantPos.Api.Models;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Services.Caching;

namespace RestaurantPos.Api.Services
{
    public class DeliveryZoneService : IDeliveryZoneService
    {
        private readonly IDeliveryZoneRepository _repository;
        private readonly ICacheService _cache;
        private readonly ITenantResolver _tenantResolver;
        private readonly IBranchContext _branchContext;
        private readonly ILogger<DeliveryZoneService> _logger;

        private string ActiveCacheKey(Guid branchId) => $"deliveryzones:active:{_tenantResolver.GetTenantId()}:{branchId}";

        public DeliveryZoneService(
            IDeliveryZoneRepository repository,
            ICacheService cache,
            ITenantResolver tenantResolver,
            IBranchContext branchContext,
            ILogger<DeliveryZoneService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<DeliveryZoneSelectDto>> GetActiveForSelectAsync(CancellationToken ct = default)
        {
            var active = await GetActiveCachedAsync(ct);
            return active.Select(MapDtoToSelectDto).ToList();
        }

        public async Task<List<DeliveryZoneSelectDto>> GetActiveForMainBranchAsync(CancellationToken ct = default)
        {
            var mainBranchId = await _repository.GetMainBranchIdAsync(_tenantResolver.GetTenantId(), ct)
                ?? throw new NotFoundException("Main branch is not configured.");
            var active = await GetActiveCachedForBranchAsync(mainBranchId, ct);
            return active.Select(MapDtoToSelectDto).ToList();
        }

        public async Task<DeliveryZoneDto?> ResolveActiveAsync(Guid id, CancellationToken ct = default)
        {
            // Only zones of the current branch are resolvable — a zone id belonging to
            // another branch fails resolution, so order intake rejects cross-branch zones.
            var active = await GetActiveCachedAsync(ct);
            return active.FirstOrDefault(z => z.Id == id);
        }

        public async Task<DeliveryZoneDto?> ResolveActiveForBranchAsync(
            Guid id,
            Guid branchId,
            CancellationToken ct = default)
        {
            var active = await GetActiveCachedForBranchAsync(branchId, ct);
            return active.FirstOrDefault(z => z.Id == id);
        }

        private async Task<List<DeliveryZoneDto>> GetActiveCachedAsync(CancellationToken ct)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);
            return await GetActiveCachedForBranchAsync(branchId, ct);
        }

        private async Task<List<DeliveryZoneDto>> GetActiveCachedForBranchAsync(Guid branchId, CancellationToken ct)
        {
            var cached = await _cache.GetOrCreateAsync(ActiveCacheKey(branchId), async () =>
            {
                var entities = await _repository.GetActiveAsync(branchId, ct);
                return entities.Select(MapToDto).ToList();
            }, slidingExpiration: TimeSpan.FromMinutes(15), absoluteExpiration: TimeSpan.FromHours(1), ct);

            return cached ?? new List<DeliveryZoneDto>();
        }

        public async Task<DeliveryZonePagedResultDto> GetPagedAsync(
            string? search,
            bool? isActive,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 200 ? 25 : pageSize;

            var branchId = await GetCurrentBranchIdAsync(ct);
            var (items, totalCount) = await _repository.GetPagedAsync(branchId, search, isActive, page, pageSize, ct);

            return new DeliveryZonePagedResultDto
            {
                Items = items.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<DeliveryZoneDto> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);
            var entity = await _repository.GetByIdAsync(id, branchId, ct)
                ?? throw new NotFoundException($"Delivery zone {id} was not found.");
            return MapToDto(entity);
        }

        public async Task<DeliveryZoneDto> CreateAsync(DeliveryZoneCreateDto dto, CancellationToken ct = default)
        {
            Validate(dto.NameEn, dto.Code, dto.DeliveryFee, dto.DeliveryCost, dto.CenterLatitude, dto.CenterLongitude, dto.RadiusMeters);

            var branchId = await GetCurrentBranchIdAsync(ct);
            if (await _repository.CodeExistsAsync(dto.Code, branchId, null, ct))
            {
                throw new ConflictException($"A delivery zone with code '{dto.Code}' already exists.");
            }

            var entity = new DeliveryZone
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantResolver.GetTenantId(),
                BranchId = branchId,
                Name = dto.NameEn.Trim(),
                NameAr = string.IsNullOrWhiteSpace(dto.NameAr) ? null : dto.NameAr.Trim(),
                Code = dto.Code.Trim(),
                DeliveryFee = dto.DeliveryFee,
                DeliveryCost = dto.DeliveryCost,
                PaymentMode = dto.PaymentMode,
                CenterLatitude = dto.CenterLatitude,
                CenterLongitude = dto.CenterLongitude,
                RadiusMeters = dto.RadiusMeters,
                IsActive = dto.IsActive,
                DisplayOrder = dto.DisplayOrder,
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            var created = await _repository.AddAsync(entity, ct);
            await InvalidateActiveCacheAsync(branchId, ct);

            _logger.LogInformation("Delivery zone {ZoneId} created with code {Code}", created.Id, created.Code);
            return MapToDto(created);
        }

        public async Task<DeliveryZoneDto> UpdateAsync(Guid id, DeliveryZoneUpdateDto dto, CancellationToken ct = default)
        {
            Validate(dto.NameEn, dto.Code, dto.DeliveryFee, dto.DeliveryCost, dto.CenterLatitude, dto.CenterLongitude, dto.RadiusMeters);

            var branchId = await GetCurrentBranchIdAsync(ct);
            var entity = await _repository.GetByIdAsync(id, branchId, ct)
                ?? throw new NotFoundException($"Delivery zone {id} was not found.");

            if (await _repository.CodeExistsAsync(dto.Code, branchId, id, ct))
            {
                throw new ConflictException($"A delivery zone with code '{dto.Code}' already exists.");
            }

            entity.Name = dto.NameEn.Trim();
            entity.NameAr = string.IsNullOrWhiteSpace(dto.NameAr) ? null : dto.NameAr.Trim();
            entity.Code = dto.Code.Trim();
            entity.DeliveryFee = dto.DeliveryFee;
            entity.DeliveryCost = dto.DeliveryCost;
            entity.PaymentMode = dto.PaymentMode;
            entity.CenterLatitude = dto.CenterLatitude;
            entity.CenterLongitude = dto.CenterLongitude;
            entity.RadiusMeters = dto.RadiusMeters;
            entity.IsActive = dto.IsActive;
            entity.DisplayOrder = dto.DisplayOrder;
            entity.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
            entity.UpdatedAt = DateTime.UtcNow;

            var updated = await _repository.UpdateAsync(entity, ct);
            await InvalidateActiveCacheAsync(branchId, ct);

            _logger.LogInformation("Delivery zone {ZoneId} updated", updated.Id);
            return MapToDto(updated);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);
            var entity = await _repository.GetByIdAsync(id, branchId, ct)
                ?? throw new NotFoundException($"Delivery zone {id} was not found.");

            await _repository.DeleteAsync(entity, ct);
            await InvalidateActiveCacheAsync(branchId, ct);

            _logger.LogInformation("Delivery zone {ZoneId} soft-deleted", id);
        }

        private static void Validate(
            string name,
            string code,
            decimal fee,
            decimal cost,
            decimal? centerLatitude,
            decimal? centerLongitude,
            int? radiusMeters)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ValidationException("Delivery zone name is required.");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ValidationException("Delivery zone code is required.");
            }

            if (fee < 0)
            {
                throw new ValidationException("Delivery fee cannot be negative.");
            }

            if (cost < 0)
            {
                throw new ValidationException("Delivery cost cannot be negative.");
            }

            if (centerLatitude.HasValue != centerLongitude.HasValue)
            {
                throw new ValidationException("Delivery zone latitude and longitude must be configured together.");
            }

            if (centerLatitude is < -90m or > 90m || centerLongitude is < -180m or > 180m)
            {
                throw new ValidationException("Delivery zone coordinates are invalid.");
            }

            if (radiusMeters.HasValue && !centerLatitude.HasValue)
            {
                throw new ValidationException("Delivery zone radius requires latitude and longitude.");
            }
        }

        private Task InvalidateActiveCacheAsync(Guid branchId, CancellationToken ct)
            => _cache.RemoveAsync(ActiveCacheKey(branchId), ct);

        private async Task<Guid> GetCurrentBranchIdAsync(CancellationToken ct)
            => (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;

        private static DeliveryZoneDto MapToDto(DeliveryZone z) => new()
        {
            Id = z.Id,
            NameEn = z.Name,
            NameAr = z.NameAr,
            Code = z.Code,
            DeliveryFee = z.DeliveryFee,
            DeliveryCost = z.DeliveryCost,
            PaymentMode = z.PaymentMode,
            CenterLatitude = z.CenterLatitude,
            CenterLongitude = z.CenterLongitude,
            RadiusMeters = z.RadiusMeters,
            IsActive = z.IsActive,
            DisplayOrder = z.DisplayOrder,
            Notes = z.Notes,
            CreatedAt = z.CreatedAt,
            UpdatedAt = z.UpdatedAt
        };

        private static DeliveryZoneSelectDto MapDtoToSelectDto(DeliveryZoneDto z) => new()
        {
            Id = z.Id,
            NameEn = z.NameEn,
            NameAr = z.NameAr,
            Code = z.Code,
            DeliveryFee = z.DeliveryFee,
            DeliveryCost = z.DeliveryCost,
            PaymentMode = z.PaymentMode,
            CenterLatitude = z.CenterLatitude,
            CenterLongitude = z.CenterLongitude,
            RadiusMeters = z.RadiusMeters,
            DisplayOrder = z.DisplayOrder
        };
    }
}
