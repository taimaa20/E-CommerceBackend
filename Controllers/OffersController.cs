using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;
using RestaurantPos.Api.Services.Time;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OffersController : ControllerBase
    {
        private const string PublicOfferCacheControl = "public, max-age=30, stale-while-revalidate=120";
        private const string SummaryMode = "summary";

        private readonly PosDbContext _context;
        private readonly ISettingsService _settingsService;
        private readonly ITenantResolver _tenantResolver;
        private readonly IBranchContext _branchContext;
        private readonly IBranchConfigurationService _branchConfigurationService;

        public OffersController(
            PosDbContext context,
            ISettingsService settingsService,
            ITenantResolver tenantResolver,
            IBranchContext branchContext,
            IBranchConfigurationService branchConfigurationService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _branchConfigurationService = branchConfigurationService ?? throw new ArgumentNullException(nameof(branchConfigurationService));
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OfferResponseDto>>> GetOffers(CancellationToken ct = default)
        {
            var dtos = (await LoadActiveOfferResponsesAsync(await ResolveStaffEnabledOfferIdsAsync(ct), ct))
                .OrderByDescending(o => o.IsAvailableNow)
                .ThenByDescending(o => o.Id)
                .ToList();

            return Ok(dtos);
        }

        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicOffers(
            [FromQuery] string? mode = null,
            CancellationToken ct = default)
        {
            if (string.Equals(mode, SummaryMode, StringComparison.OrdinalIgnoreCase))
            {
                Response.Headers.CacheControl = PublicOfferCacheControl;
                return Ok(await GetPublicOfferSummariesAsync(ct));
            }

            var enabledOfferIds = await ResolvePublicEnabledOfferIdsAsync(ct);
            var offers = await _context.Offers
                .AsNoTracking()
                .Include(o => o.OfferProducts)
                    .ThenInclude(op => op.Product)
                .Where(o => o.IsActive && enabledOfferIds.Contains(o.Id))
                .OrderByDescending(o => o.Id)
                .ToListAsync(ct);
            var localNow = await GetRestaurantLocalNowAsync(ct);

            return Ok(offers
                .Select(offer => MapOfferResponse(offer, localNow))
                .OrderByDescending(o => o.IsAvailableNow)
                .ThenByDescending(o => o.Id)
                .ToList());
        }

        [HttpGet("public/available")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<OfferResponseDto>>> GetPublicAvailableOffers(CancellationToken ct = default)
        {
            var dtos = (await LoadActiveOfferResponsesAsync(await ResolvePublicEnabledOfferIdsAsync(ct), ct))
                .Where(o => o.IsAvailableNow)
                .ToList();

            return Ok(dtos);
        }

        [HttpGet("all")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<IEnumerable<OfferResponseDto>>> GetAllOffers(CancellationToken ct = default)
        {
            var localNow = await GetRestaurantLocalNowAsync(ct);
            var offers = await _context.Offers
                .AsNoTracking()
                .Include(o => o.OfferProducts)
                    .ThenInclude(op => op.Product)
                .OrderByDescending(o => o.Id)
                .ToListAsync(ct);

            return Ok(offers.Select(offer => MapOfferResponse(offer, localNow)).ToList());
        }

        [HttpPost]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<OfferResponseDto>> CreateOffer(
            CreateOfferRequest request,
            CancellationToken ct = default)
        {
            var validationError = await ValidateOfferRequestAsync(request, ct);
            if (validationError is not null)
            {
                return validationError;
            }

            var products = await LoadOfferProductsAsync(request.Products, ct);
            var finalPrice = AvailabilityHelper.ComputeFinalPrice(
                request.DiscountType,
                request.DiscountValue,
                products.Select(p => (p.EffectivePrice, p.Quantity)).ToList());

            var offer = new Offer
            {
                Name = request.Name.Trim(),
                NameAr = request.NameAr.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                DescriptionAr = string.IsNullOrWhiteSpace(request.DescriptionAr) ? null : request.DescriptionAr.Trim(),
                ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
                DiscountType = (OfferDiscountType)request.DiscountType,
                DiscountValue = request.DiscountValue,
                FinalPrice = finalPrice,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                IsActive = true,
                OfferProducts = products.Select(product => new OfferProduct
                {
                    ProductId = product.Id,
                    Quantity = product.Quantity
                }).ToList()
            };

            _context.Offers.Add(offer);
            await _context.SaveChangesAsync(ct);

            // New offers auto-enable on the Main Branch only — other branches opt in via
            // Branch Configuration, so Main Branch behavior stays identical to legacy.
            await _branchConfigurationService.EnsureMainBranchOfferAsync(_tenantResolver.GetTenantId(), offer.Id, ct);

            var createdOffer = await LoadOfferByIdAsync(offer.Id, ct);
            var localNow = await GetRestaurantLocalNowAsync(ct);
            return CreatedAtAction(nameof(GetOffers), new { id = offer.Id }, MapOfferResponse(createdOffer!, localNow));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<OfferResponseDto>> UpdateOffer(
            int id,
            CreateOfferRequest request,
            CancellationToken ct = default)
        {
            var offer = await _context.Offers
                .Include(o => o.OfferProducts)
                .FirstOrDefaultAsync(o => o.Id == id, ct);

            if (offer is null)
            {
                return NotFound();
            }

            var validationError = await ValidateOfferRequestAsync(request, ct);
            if (validationError is not null)
            {
                return validationError;
            }

            var products = await LoadOfferProductsAsync(request.Products, ct);
            var finalPrice = AvailabilityHelper.ComputeFinalPrice(
                request.DiscountType,
                request.DiscountValue,
                products.Select(p => (p.EffectivePrice, p.Quantity)).ToList());

            offer.Name = request.Name.Trim();
            offer.NameAr = request.NameAr.Trim();
            offer.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            offer.DescriptionAr = string.IsNullOrWhiteSpace(request.DescriptionAr) ? null : request.DescriptionAr.Trim();
            offer.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
            offer.DiscountType = (OfferDiscountType)request.DiscountType;
            offer.DiscountValue = request.DiscountValue;
            offer.FinalPrice = finalPrice;
            offer.StartDate = request.StartDate;
            offer.EndDate = request.EndDate;
            offer.StartTime = request.StartTime;
            offer.EndTime = request.EndTime;

            _context.OfferProducts.RemoveRange(offer.OfferProducts);
            offer.OfferProducts = products.Select(product => new OfferProduct
            {
                OfferId = offer.Id,
                ProductId = product.Id,
                Quantity = product.Quantity
            }).ToList();

            await _context.SaveChangesAsync(ct);

            var updatedOffer = await LoadOfferByIdAsync(offer.Id, ct);
            var localNow = await GetRestaurantLocalNowAsync(ct);
            return Ok(MapOfferResponse(updatedOffer!, localNow));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> DeleteOffer(int id, CancellationToken ct = default)
        {
            var offer = await _context.Offers
                .Include(o => o.OfferProducts)
                .FirstOrDefaultAsync(o => o.Id == id, ct);
            if (offer is null)
            {
                return NotFound();
            }

            // Config rows hold a Restrict FK to the offer — clear them before the hard delete.
            await _branchConfigurationService.RemoveOfferBranchConfigurationsAsync(offer.Id, ct);
            _context.OfferProducts.RemoveRange(offer.OfferProducts);
            _context.Offers.Remove(offer);
            await _context.SaveChangesAsync(ct);

            return Ok();
        }

        [HttpPut("{id}/deactivate")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<IActionResult> DeactivateOffer(int id, CancellationToken ct = default)
        {
            var offer = await _context.Offers.FirstOrDefaultAsync(o => o.Id == id, ct);
            if (offer is null)
            {
                return NotFound();
            }

            offer.IsActive = false;
            await _context.SaveChangesAsync(ct);

            return Ok();
        }

        [HttpPut("{id}/reactivate")]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<OfferResponseDto>> ReactivateOffer(int id, CancellationToken ct = default)
        {
            var offer = await _context.Offers.FirstOrDefaultAsync(o => o.Id == id, ct);
            if (offer is null)
            {
                return NotFound();
            }

            offer.IsActive = true;
            await _context.SaveChangesAsync(ct);

            var reactivatedOffer = await LoadOfferByIdAsync(offer.Id, ct);
            var localNow = await GetRestaurantLocalNowAsync(ct);
            return Ok(MapOfferResponse(reactivatedOffer!, localNow));
        }

        private async Task<Offer?> LoadOfferByIdAsync(int id, CancellationToken ct)
        {
            return await _context.Offers
                .AsNoTracking()
                .Include(o => o.OfferProducts)
                    .ThenInclude(op => op.Product)
                .FirstOrDefaultAsync(o => o.Id == id, ct);
        }

        private async Task<List<OfferResponseDto>> LoadActiveOfferResponsesAsync(
            IReadOnlySet<int> enabledOfferIds,
            CancellationToken ct)
        {
            var localNow = await GetRestaurantLocalNowAsync(ct);
            var offers = await _context.Offers
                .AsNoTracking()
                .Include(o => o.OfferProducts)
                    .ThenInclude(op => op.Product)
                .Where(o => o.IsActive && enabledOfferIds.Contains(o.Id))
                .OrderByDescending(o => o.Id)
                .ToListAsync(ct);

            return offers.Select(offer => MapOfferResponse(offer, localNow)).ToList();
        }

        private async Task<IReadOnlySet<int>> ResolveStaffEnabledOfferIdsAsync(CancellationToken ct)
        {
            var branchId = (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;
            return await _branchConfigurationService.GetEnabledOfferIdsAsync(branchId, ct);
        }

        // Public offers always belong to the Main Branch. An optional customer/mobile
        // bearer token must not route this anonymous contract through staff assignments.
        private async Task<IReadOnlySet<int>> ResolvePublicEnabledOfferIdsAsync(CancellationToken ct)
        {
            var tenantId = _tenantResolver.GetTenantId();
            var branchId = await _context.Branches
                .AsNoTracking()
                .Where(b => b.TenantId == tenantId && b.IsMainBranch)
                .Select(b => (Guid?)b.Id)
                .FirstOrDefaultAsync(ct);
            if (!branchId.HasValue)
                return new HashSet<int>();
            return await _branchConfigurationService.GetEnabledOfferIdsAsync(branchId.Value, ct);
        }

        private async Task<List<PublicOfferDto>> GetPublicOfferSummariesAsync(CancellationToken ct)
        {
            var localNow = await GetRestaurantLocalNowAsync(ct);
            var today = DateOnly.FromDateTime(localNow);
            var yesterday = today.AddDays(-1);

            var enabledOfferIds = await ResolvePublicEnabledOfferIdsAsync(ct);
            var offers = await _context.Offers
                .AsNoTracking()
                .Include(o => o.OfferProducts)
                    .ThenInclude(op => op.Product)
                .Where(o => o.IsActive && enabledOfferIds.Contains(o.Id) && o.StartDate <= today && o.EndDate >= yesterday)
                .OrderByDescending(o => o.Id)
                .ToListAsync(ct);

            var summaries = offers
                .Where(o => AvailabilityHelper.IsAvailableAt(
                    o.StartDate,
                    o.EndDate,
                    o.StartTime,
                    o.EndTime,
                    localNow))
                .Select(o => new PublicOfferDto
                {
                    Id = o.Id,
                    Name = o.Name,
                    NameAr = o.NameAr,
                    Description = o.Description,
                    DescriptionAr = o.DescriptionAr,
                    Price = o.FinalPrice,
                    OriginalPrice = o.OfferProducts.Sum(op => (op.Product.DiscountedPrice ?? op.Product.BasePrice) * op.Quantity),
                    ImageUrl = o.ImageUrl
                })
                .ToList();

            summaries.ForEach(o => o.ImageUrl = IsBase64ImageUrl(o.ImageUrl) ? null : o.ImageUrl);
            return summaries;
        }

        private static bool IsBase64ImageUrl(string? imageUrl)
        {
            return imageUrl?.StartsWith("data:", StringComparison.OrdinalIgnoreCase) == true;
        }

        private async Task<ActionResult?> ValidateOfferRequestAsync(
            CreateOfferRequest request,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.NameAr))
            {
                return BadRequest(new { message = "Offer name is required in both English and Arabic." });
            }

            if (!string.IsNullOrWhiteSpace(request.Description) && request.Description.Length > 1000)
            {
                return BadRequest(new { message = "Offer description cannot exceed 1000 characters." });
            }

            if (!string.IsNullOrWhiteSpace(request.DescriptionAr) && request.DescriptionAr.Length > 1000)
            {
                return BadRequest(new { message = "Arabic offer description cannot exceed 1000 characters." });
            }

            if (request.EndDate < request.StartDate)
            {
                return BadRequest(new { message = "End date must be greater than or equal to start date." });
            }

            if (request.StartTime == request.EndTime)
            {
                return BadRequest(new { message = "Start time and end time cannot be the same." });
            }

            if (request.DiscountType is < 0 or > 1)
            {
                return BadRequest(new { message = "Discount type must be 0 (Fixed) or 1 (Percentage)." });
            }

            if (request.DiscountValue <= 0)
            {
                return BadRequest(new { message = "Discount value must be greater than zero." });
            }

            if (request.DiscountType == 1 && request.DiscountValue > 100)
            {
                return BadRequest(new { message = "Percentage discount must not exceed 100." });
            }

            if (request.Products is null || request.Products.Count == 0)
            {
                return BadRequest(new { message = "At least one product is required." });
            }

            if (request.Products.Any(p => p.Quantity <= 0))
            {
                return BadRequest(new { message = "All offer product quantities must be greater than zero." });
            }

            var requestedIds = request.Products.Select(p => p.ProductId).Distinct().ToList();
            var existingIds = await _context.Products
                .Where(p => requestedIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(ct);

            if (existingIds.Count != requestedIds.Count)
            {
                return BadRequest(new { message = "One or more selected products do not exist." });
            }

            return null;
        }

        private async Task<List<(Guid Id, decimal EffectivePrice, int Quantity)>> LoadOfferProductsAsync(
            List<OfferProductItemRequest> requestProducts,
            CancellationToken ct)
        {
            var quantitiesByProductId = requestProducts
                .GroupBy(p => p.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            var products = await _context.Products
                .Where(p => quantitiesByProductId.Keys.Contains(p.Id))
                .Select(p => new
                {
                    p.Id,
                    EffectivePrice = p.DiscountedPrice ?? p.BasePrice
                })
                .ToListAsync(ct);

            return products
                .Select(p => (p.Id, p.EffectivePrice, quantitiesByProductId[p.Id]))
                .ToList();
        }

        private static decimal GetEffectiveOfferProductPrice(Product product)
        {
            return product.DiscountedPrice ?? product.BasePrice;
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

        private static OfferResponseDto MapOfferResponse(Offer offer, DateTime localNow)
        {
            var originalTotal = offer.OfferProducts.Sum(op => GetEffectiveOfferProductPrice(op.Product) * op.Quantity);
            var isAvailableNow = offer.IsActive && AvailabilityHelper.IsAvailableAt(
                offer.StartDate,
                offer.EndDate,
                offer.StartTime,
                offer.EndTime,
                localNow);

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
                IsAvailableNow = isAvailableNow,
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
                    Price = GetEffectiveOfferProductPrice(op.Product),
                    IsSoon = op.Product.IsSoon
                }).ToList()
            };
        }
    }
}
