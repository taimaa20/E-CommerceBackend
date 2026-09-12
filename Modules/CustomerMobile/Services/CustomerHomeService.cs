using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;
using RestaurantPos.Api.Services.Time;

namespace RestaurantPos.Api.Modules.CustomerMobile.Services
{
    public class CustomerHomeService : ICustomerHomeService
    {
        private const int HomeCategoryLimit = 12;
        private const int HomeProductLimit = 10;
        private const int HomeOfferLimit = 10;
        private const int LastOrderLimit = 5;
        private const int PopularLookbackDays = 30;

        private readonly PosDbContext _context;
        private readonly IMenuService _menuService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ISettingsService _settingsService;
        private readonly ITenantResolver _tenantResolver;

        public CustomerHomeService(
            PosDbContext context,
            IMenuService menuService,
            IHttpContextAccessor httpContextAccessor,
            ISettingsService settingsService,
            ITenantResolver tenantResolver)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
        }

        public async Task<CustomerHomeDto> GetHomeAsync(string language, CancellationToken ct)
        {
            var normalizedLanguage = NormalizeLanguage(language);
            var menu = await _menuService.GetPublicMenuAsync(normalizedLanguage);
            var offers = await LoadAvailableOffersAsync(ct);

            return new CustomerHomeDto
            {
                Banners = await LoadBannersAsync(normalizedLanguage, ct),
                Categories = menu.Categories.Take(HomeCategoryLimit).ToList(),
                PopularProducts = await ResolvePopularProductsAsync(menu.Products, ct),
                FeaturedProducts = ResolveFeaturedProducts(menu.Products),
                AvailableOffers = offers.Take(HomeOfferLimit).ToList(),
                LastOrders = await LoadLastOrdersAsync(ct)
            };
        }

        public async Task<CustomerSearchResponseDto> SearchAsync(string keyword, string language, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new CustomerSearchResponseDto();

            var term = keyword.Trim();
            var normalizedLanguage = NormalizeLanguage(language);
            var menu = await _menuService.GetPublicMenuAsync(normalizedLanguage);
            var offers = await LoadAvailableOffersAsync(ct);

            return new CustomerSearchResponseDto
            {
                Products = menu.Products.Where(p => Matches(term, p.Name, p.NameAr, p.Description, p.DescriptionAr)).ToList(),
                Categories = menu.Categories.Where(c => Matches(term, c.Name, c.NameAr, c.Description)).ToList(),
                Offers = offers.Where(o => Matches(term, o.Name, o.NameAr, o.Description, o.DescriptionAr)).ToList()
            };
        }

        public async Task<PublicProductDto> GetProductAsync(Guid id, string language, CancellationToken ct)
        {
            var menu = await _menuService.GetPublicMenuAsync(NormalizeLanguage(language));
            return menu.Products.FirstOrDefault(p => p.Id == id)
                ?? throw new NotFoundException("Product was not found.");
        }

        private async Task<List<CustomerHomeBannerDto>> LoadBannersAsync(string language, CancellationToken ct)
        {
            var isArabic = language == "ar";
            var settings = await _context.TenantMenuSettings
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(settings?.HeroBannerUrl))
                return new List<CustomerHomeBannerDto>();

            return new List<CustomerHomeBannerDto>
            {
                new()
                {
                    Title = isArabic ? settings.HeroBannerTitleAr : settings.HeroBannerTitle,
                    TitleAr = settings.HeroBannerTitleAr,
                    ImageUrl = settings.HeroBannerUrl
                }
            };
        }

        private async Task<List<PublicProductDto>> ResolvePopularProductsAsync(
            List<PublicProductDto> products,
            CancellationToken ct)
        {
            var cutoff = DateTime.UtcNow.AddDays(-PopularLookbackDays);
            var productIds = await _context.OrderItems
                .AsNoTracking()
                .Where(i => i.Order.CreatedAt >= cutoff && i.Order.Status != OrderStatus.Cancelled)
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Quantity, ct);

            var popular = products
                .Where(p => productIds.ContainsKey(p.Id))
                .OrderByDescending(p => productIds[p.Id])
                .Take(HomeProductLimit)
                .ToList();

            return popular.Count > 0
                ? popular
                : products.Where(p => !p.IsSoon).Take(HomeProductLimit).ToList();
        }

        private static List<PublicProductDto> ResolveFeaturedProducts(List<PublicProductDto> products)
        {
            var discounted = products
                .Where(p => !p.IsSoon && p.OriginalPrice.HasValue)
                .Take(HomeProductLimit)
                .ToList();

            if (discounted.Count >= HomeProductLimit)
                return discounted;

            discounted.AddRange(products
                .Where(p => !p.IsSoon && !discounted.Any(d => d.Id == p.Id))
                .Take(HomeProductLimit - discounted.Count));

            return discounted;
        }

        private async Task<List<CustomerOrderListItemDto>> LoadLastOrdersAsync(CancellationToken ct)
        {
            var customerId = ResolveOptionalCustomerId();
            if (!customerId.HasValue)
                return new List<CustomerOrderListItemDto>();

            return await _context.CustomerMobileOrders
                .AsNoTracking()
                .Where(m => m.CustomerId == customerId.Value)
                .OrderByDescending(m => m.CreatedAt)
                .Take(LastOrderLimit)
                .Select(m => new CustomerOrderListItemDto
                {
                    Id = m.Id,
                    OrderId = m.OrderId,
                    OrderNumber = m.Order.DisplayOrderNumber ?? m.OrderNumber,
                    OrderType = m.OrderType.ToString(),
                    Status = m.Order.Status.ToString(),
                    TotalAmount = m.Order.TotalAmount,
                    TotalItemsCount = m.Order.OrderItems.Select(i => (int?)i.Quantity).Sum() ?? 0,
                    CreatedAt = m.Order.CreatedAt
                })
                .ToListAsync(ct);
        }

        private async Task<List<OfferResponseDto>> LoadAvailableOffersAsync(CancellationToken ct)
        {
            var localNow = await GetRestaurantLocalNowAsync(ct);
            var offers = await _context.Offers
                .AsNoTracking()
                .Include(o => o.OfferProducts)
                    .ThenInclude(op => op.Product)
                .Where(o => o.IsActive)
                .OrderByDescending(o => o.Id)
                .ToListAsync(ct);

            return offers
                .Select(offer => MapOffer(offer, localNow))
                .Where(o => o.IsAvailableNow)
                .ToList();
        }

        private Guid? ResolveOptionalCustomerId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true || !user.IsInRole(AppRoleNames.Customer))
                return null;

            var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }

        private async Task<DateTime> GetRestaurantLocalNowAsync(CancellationToken ct)
        {
            var settings = await _settingsService.GetSettingsAsync(ResolveTenantIdOrNull(), ct);
            var timeZone = RestaurantTimeZone.Resolve(settings.TimeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        }

        private Guid? ResolveTenantIdOrNull()
        {
            try
            {
                return _tenantResolver.GetTenantId();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static OfferResponseDto MapOffer(Offer offer, DateTime localNow)
        {
            var originalTotal = offer.OfferProducts.Sum(op => GetEffectivePrice(op.Product) * op.Quantity);
            return new OfferResponseDto
            {
                Id = offer.Id,
                Name = offer.Name,
                NameAr = offer.NameAr,
                Description = offer.Description,
                DescriptionAr = offer.DescriptionAr,
                ImageUrl = offer.ImageUrl,
                DiscountType = (int)offer.DiscountType,
                DiscountValue = offer.DiscountValue,
                FinalPrice = offer.FinalPrice,
                OriginalTotal = originalTotal,
                StartDate = offer.StartDate,
                EndDate = offer.EndDate,
                StartTime = offer.StartTime,
                EndTime = offer.EndTime,
                IsActive = offer.IsActive,
                IsAvailableNow = offer.IsActive && AvailabilityHelper.IsAvailableAt(
                    offer.StartDate,
                    offer.EndDate,
                    offer.StartTime,
                    offer.EndTime,
                    localNow),
                Products = offer.OfferProducts.Select(op => new OfferProductDto
                {
                    ProductId = op.ProductId,
                    Name = op.Product.Name,
                    NameAr = op.Product.NameAr ?? string.Empty,
                    Description = op.Product.Description,
                    DescriptionAr = op.Product.DescriptionAr,
                    Calories = op.Product.Calories,
                    Allergens = (int)op.Product.Allergens,
                    Quantity = op.Quantity,
                    Price = GetEffectivePrice(op.Product),
                    IsSoon = op.Product.IsSoon
                }).ToList()
            };
        }

        private static bool Matches(string keyword, params string?[] values)
            => values.Any(v => !string.IsNullOrWhiteSpace(v) &&
                v.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        private static decimal GetEffectivePrice(Product product)
            => product.DiscountedPrice ?? product.BasePrice;

        private static string NormalizeLanguage(string? language)
            => string.Equals(language?.Trim(), "ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en";
    }
}
