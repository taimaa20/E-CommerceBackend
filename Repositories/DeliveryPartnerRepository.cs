using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public class DeliveryPartnerRepository : IDeliveryPartnerRepository
    {
        private readonly PosDbContext _context;

        public DeliveryPartnerRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<DeliveryPartnerDto>> GetActiveAsync(CancellationToken ct = default)
        {
            return await ProjectPartners(_context.DeliveryPartners.AsNoTracking())
                .Where(p => p.Status == DeliveryPartnerStatus.Active)
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Name)
                .ToListAsync(ct);
        }

        public async Task<(List<DeliveryPartnerDto> Items, int TotalCount)> GetPagedAsync(
            string? search,
            DeliveryPartnerStatus? status,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = _context.DeliveryPartners.AsNoTracking();
            query = ApplyPartnerFilters(query, search, status);

            var totalCount = await query.CountAsync(ct);
            var items = await ProjectPartners(query)
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, totalCount);
        }

        public async Task<DeliveryPartner?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _context.DeliveryPartners
                .AsNoTracking()
                .Include(p => p.ProductMappings)
                    .ThenInclude(m => m.Product)
                .FirstOrDefaultAsync(p => p.Id == id, ct);
        }

        public async Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken ct = default)
        {
            var normalized = code.Trim().ToLower();
            return await _context.DeliveryPartners
                .AsNoTracking()
                .AnyAsync(p => p.Code.ToLower() == normalized && (!excludeId.HasValue || p.Id != excludeId.Value), ct);
        }

        public async Task<DeliveryPartner> AddAsync(DeliveryPartner partner, CancellationToken ct = default)
        {
            _context.DeliveryPartners.Add(partner);
            await _context.SaveChangesAsync(ct);
            return partner;
        }

        public async Task<DeliveryPartner> UpdateAsync(DeliveryPartner partner, CancellationToken ct = default)
        {
            _context.DeliveryPartners.Update(partner);
            await _context.SaveChangesAsync(ct);
            return partner;
        }

        public async Task DeleteAsync(DeliveryPartner partner, CancellationToken ct = default)
        {
            partner.DeletedAt = DateTime.UtcNow;
            partner.Status = DeliveryPartnerStatus.Disabled;
            _context.DeliveryPartners.Update(partner);
            await _context.SaveChangesAsync(ct);
        }

        public Task<bool> ProductExistsAsync(Guid productId, CancellationToken ct = default)
            => _context.Products.AsNoTracking().AnyAsync(p => p.Id == productId, ct);

        public Task<bool> PartnerExistsAsync(Guid partnerId, CancellationToken ct = default)
            => _context.DeliveryPartners.AsNoTracking().AnyAsync(p => p.Id == partnerId, ct);

        public async Task<DeliveryPartnerProduct?> GetMappingAsync(Guid productId, Guid partnerId, CancellationToken ct = default)
        {
            return await _context.DeliveryPartnerProducts
                .FirstOrDefaultAsync(m => m.ProductId == productId && m.DeliveryPartnerId == partnerId, ct);
        }

        public async Task<DeliveryPartnerProduct> SaveMappingAsync(DeliveryPartnerProduct mapping, CancellationToken ct = default)
        {
            if (_context.Entry(mapping).State == EntityState.Detached)
            {
                _context.DeliveryPartnerProducts.Add(mapping);
            }

            await _context.SaveChangesAsync(ct);
            return mapping;
        }

        public async Task<List<DeliveryPartnerProductMappingDto>> GetProductMappingsAsync(
            Guid productId,
            bool activePartnersOnly,
            CancellationToken ct = default)
        {
            var query = ProductMappingQuery().Where(row => row.ProductId == productId);
            if (activePartnersOnly)
            {
                query = query.Where(row => row.PartnerStatus == DeliveryPartnerStatus.Active);
            }

            return await query
                .OrderBy(row => row.PartnerName)
                .ToListAsync(ct);
        }

        public async Task<(List<DeliveryPartnerProductMappingDto> Items, int TotalCount)> GetPartnerProductMappingsAsync(
            Guid partnerId,
            string? search,
            bool? enabledOnly,
            Guid? categoryId,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = ProductMappingQuery().Where(row => row.PartnerId == partnerId);
            query = ApplyProductMappingFilters(query, search, enabledOnly, categoryId);

            var totalCount = await query.CountAsync(ct);
            var items = await query
                .OrderBy(row => row.ProductName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, totalCount);
        }

        public async Task AddActivityLogAsync(DeliveryPartnerActivityLog log, CancellationToken ct = default)
        {
            _context.DeliveryPartnerActivityLogs.Add(log);
            await _context.SaveChangesAsync(ct);
        }

        private static IQueryable<DeliveryPartner> ApplyPartnerFilters(
            IQueryable<DeliveryPartner> query,
            string? search,
            DeliveryPartnerStatus? status)
        {
            if (status.HasValue)
            {
                query = query.Where(p => p.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p =>
                    EF.Functions.ILike(p.Name, $"%{term}%") ||
                    (p.NameAr != null && EF.Functions.ILike(p.NameAr, $"%{term}%")) ||
                    EF.Functions.ILike(p.Code, $"%{term}%"));
            }

            return query;
        }

        private static IQueryable<DeliveryPartnerDto> ProjectPartners(IQueryable<DeliveryPartner> query)
            => query.Select(p => new DeliveryPartnerDto
            {
                Id = p.Id,
                Name = p.Name,
                NameAr = p.NameAr,
                Code = p.Code,
                Description = p.Description,
                DescriptionAr = p.DescriptionAr,
                LogoUrl = p.LogoUrl,
                Status = p.Status,
                DefaultPricingRuleType = p.DefaultPricingRuleType,
                DefaultPricingRuleValue = p.DefaultPricingRuleValue,
                DeliveryCostRuleType = p.DeliveryCostRuleType,
                DefaultDeliveryCostValue = p.DefaultDeliveryCostValue,
                CostSharingMode = p.CostSharingMode,
                CostSharingScope = p.CostSharingScope,
                CostSharingCommissionPercentage = p.CostSharingCommissionPercentage,
                CostSharingRestaurantPercentage = p.CostSharingRestaurantPercentage,
                CostSharingCounterpartyPercentage = p.CostSharingCounterpartyPercentage,
                IntegrationEnabled = p.IntegrationEnabled,
                IntegrationSettingsJson = p.IntegrationSettingsJson,
                HasCredentials = p.CredentialsJson != null && p.CredentialsJson != string.Empty,
                SortOrder = p.SortOrder,
                ProductMappingCount = p.ProductMappings.Count(m => m.Product.IsActive),
                EnabledProductCount = p.ProductMappings.Count(m => m.IsEnabled && m.IsAvailable && m.Product.IsActive),
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            });

        private IQueryable<DeliveryPartnerProductMappingDto> ProductMappingQuery()
            => from product in _context.Products.AsNoTracking()
               from partner in _context.DeliveryPartners.AsNoTracking()
               join mapping in _context.DeliveryPartnerProducts.AsNoTracking()
                    on new { ProductId = product.Id, PartnerId = partner.Id }
                    equals new { mapping.ProductId, PartnerId = mapping.DeliveryPartnerId }
                    into mappingJoin
               from mapping in mappingJoin.DefaultIfEmpty()
               select new DeliveryPartnerProductMappingDto
               {
                   Id = mapping == null ? null : mapping.Id,
                   PartnerId = partner.Id,
                   PartnerName = partner.Name,
                   PartnerNameAr = partner.NameAr,
                   PartnerCode = partner.Code,
                   PartnerStatus = partner.Status,
                   PartnerDefaultPricingRuleType = partner.DefaultPricingRuleType,
                   PartnerDefaultPricingRuleValue = partner.DefaultPricingRuleValue,
                   ProductId = product.Id,
                   ProductName = product.Name,
                   ProductNameAr = product.NameAr,
                   ProductCategoryId = product.CategoryId,
                   ProductCategoryName = product.Category == null ? null : product.Category.Name,
                   ProductCategoryNameAr = product.Category == null ? null : product.Category.NameAr,
                   ProductBasePrice = product.BasePrice,
                   ProductIsActive = product.IsActive,
                   PartnerProductId = mapping == null ? null : mapping.PartnerProductId,
                   IsEnabled = mapping != null && mapping.IsEnabled,
                   IsAvailable = mapping == null || mapping.IsAvailable,
                   PricingRuleType = mapping == null ? null : mapping.PricingRuleType,
                   PricingRuleValue = mapping == null ? null : mapping.PricingRuleValue,
                   CustomPrice = mapping == null ? null : mapping.CustomPrice,
                   AvailableStartDate = mapping == null ? null : mapping.AvailableStartDate,
                   AvailableEndDate = mapping == null ? null : mapping.AvailableEndDate,
                   AvailableFrom = mapping == null ? null : mapping.AvailableFrom,
                   AvailableTo = mapping == null ? null : mapping.AvailableTo,
                   UpdatedAt = mapping == null ? null : mapping.UpdatedAt
               };

        private static IQueryable<DeliveryPartnerProductMappingDto> ApplyProductMappingFilters(
            IQueryable<DeliveryPartnerProductMappingDto> query,
            string? search,
            bool? enabledOnly,
            Guid? categoryId)
        {
            if (enabledOnly.HasValue)
            {
                query = query.Where(row => row.IsEnabled == enabledOnly.Value);
            }

            if (categoryId.HasValue)
            {
                query = query.Where(row => row.ProductCategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(row =>
                    EF.Functions.ILike(row.ProductName, $"%{term}%") ||
                    (row.ProductNameAr != null && EF.Functions.ILike(row.ProductNameAr, $"%{term}%")) ||
                    (row.ProductCategoryName != null && EF.Functions.ILike(row.ProductCategoryName, $"%{term}%")) ||
                    (row.ProductCategoryNameAr != null && EF.Functions.ILike(row.ProductCategoryNameAr, $"%{term}%")) ||
                    (row.PartnerProductId != null && EF.Functions.ILike(row.PartnerProductId, $"%{term}%")));
            }

            return query;
        }

    }
}
