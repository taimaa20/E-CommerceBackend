using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = AppRoleGroups.AdminOnly)]
    public class RecipeItemAlternativesController : ControllerBase
    {
        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenantResolver;
        private readonly ILogger<RecipeItemAlternativesController> _logger;

        public RecipeItemAlternativesController(
            PosDbContext context,
            ITenantResolver tenantResolver,
            ILogger<RecipeItemAlternativesController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: api/RecipeItemAlternatives/recipeitem/{recipeItemId}
        [HttpGet("recipeitem/{recipeItemId:guid}")]
        [Authorize]
        public async Task<ActionResult<List<RecipeItemAlternativeDto>>> GetByRecipeItem(Guid recipeItemId, CancellationToken ct)
        {
            var alts = await _context.RecipeItemAlternatives
                .Include(a => a.RawMaterial)
                .Include(a => a.RecipeItem)
                    .ThenInclude(ri => ri.RawMaterial)
                .Include(a => a.RecipeItem)
                    .ThenInclude(ri => ri.Product)
                .Where(a => a.RecipeItemId == recipeItemId)
                .OrderBy(a => a.SortOrder)
                .ThenBy(a => a.Name)
                .ToListAsync(ct);

            return Ok(alts.Select(MapToDto).ToList());
        }

        // POST: api/RecipeItemAlternatives
        [HttpPost]
        public async Task<ActionResult<RecipeItemAlternativeDto>> Create(
            [FromBody] RecipeItemAlternativeCreateDto dto,
            CancellationToken ct)
        {
            var tenantId = _tenantResolver.GetTenantId();

            var recipeItem = await _context.RecipeItems
                .Include(ri => ri.RawMaterial)
                .Include(ri => ri.Product)
                .FirstOrDefaultAsync(ri => ri.Id == dto.RecipeItemId, ct);
            if (recipeItem == null)
                return NotFound("Recipe item not found.");

            var material = await _context.RawMaterials.FindAsync(new object[] { dto.RawMaterialId }, ct);
            if (material == null)
                return NotFound("Raw material not found.");

            var pricingType = MapPricingType(dto.AlternativePricingType);
            var validationError = ValidatePricing(pricingType, dto.FixedOverridePrice);
            if (validationError != null)
                return BadRequest(validationError);

            if (dto.IsDefault)
                await ClearDefaultFlagAsync(dto.RecipeItemId, null, ct);

            var alt = new RecipeItemAlternative
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RecipeItemId = dto.RecipeItemId,
                RawMaterialId = dto.RawMaterialId,
                RawMaterial = material,
                RecipeItem = recipeItem,
                Name = string.IsNullOrWhiteSpace(dto.Name) ? null : dto.Name.Trim(),
                NameAr = string.IsNullOrWhiteSpace(dto.NameAr) ? null : dto.NameAr!.Trim(),
                Amount = dto.Amount,
                PriceAdjustment = dto.PriceAdjustment,
                PricingType = pricingType,
                CustomerAdditionalPrice = ResolveCustomerAdditionalPrice(pricingType, dto.CustomerAdditionalPrice),
                FixedOverridePrice = ResolveFixedOverridePrice(pricingType, dto.FixedOverridePrice),
                IsDefault = dto.IsDefault,
                IsActive = true,
                SortOrder = dto.SortOrder,
            };

            _context.RecipeItemAlternatives.Add(alt);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "RecipeItemAlternative {AlternativeId} created for recipe item {RecipeItemId} (material {RawMaterialId})",
                alt.Id, dto.RecipeItemId, dto.RawMaterialId);

            return CreatedAtAction(nameof(GetByRecipeItem), new { recipeItemId = dto.RecipeItemId }, MapToDto(alt));
        }

        // PUT: api/RecipeItemAlternatives/{id}
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<RecipeItemAlternativeDto>> Update(
            Guid id,
            [FromBody] RecipeItemAlternativeUpdateDto dto,
            CancellationToken ct)
        {
            var alt = await _context.RecipeItemAlternatives
                .Include(a => a.RawMaterial)
                .Include(a => a.RecipeItem)
                    .ThenInclude(ri => ri.RawMaterial)
                .Include(a => a.RecipeItem)
                    .ThenInclude(ri => ri.Product)
                .FirstOrDefaultAsync(a => a.Id == id, ct);

            if (alt == null)
                return NotFound();

            var material = await _context.RawMaterials.FindAsync(new object[] { dto.RawMaterialId }, ct);
            if (material == null)
                return NotFound("Raw material not found.");

            var pricingType = MapPricingType(dto.AlternativePricingType);
            var validationError = ValidatePricing(pricingType, dto.FixedOverridePrice);
            if (validationError != null)
                return BadRequest(validationError);

            if (dto.IsDefault && !alt.IsDefault)
                await ClearDefaultFlagAsync(alt.RecipeItemId, id, ct);

            alt.RawMaterialId = dto.RawMaterialId;
            alt.RawMaterial = material;
            alt.Name = string.IsNullOrWhiteSpace(dto.Name) ? null : dto.Name.Trim();
            alt.NameAr = string.IsNullOrWhiteSpace(dto.NameAr) ? null : dto.NameAr!.Trim();
            alt.Amount = dto.Amount;
            alt.PriceAdjustment = dto.PriceAdjustment;
            alt.PricingType = pricingType;
            alt.CustomerAdditionalPrice = ResolveCustomerAdditionalPrice(pricingType, dto.CustomerAdditionalPrice);
            alt.FixedOverridePrice = ResolveFixedOverridePrice(pricingType, dto.FixedOverridePrice);
            alt.IsDefault = dto.IsDefault;
            alt.IsActive = dto.IsActive;
            alt.SortOrder = dto.SortOrder;

            await _context.SaveChangesAsync(ct);

            return Ok(MapToDto(alt));
        }

        // DELETE: api/RecipeItemAlternatives/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var alt = await _context.RecipeItemAlternatives.FindAsync(new object[] { id }, ct);
            if (alt == null)
                return NotFound();

            _context.RecipeItemAlternatives.Remove(alt);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("RecipeItemAlternative {AlternativeId} deleted", id);
            return Ok();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private async Task ClearDefaultFlagAsync(Guid recipeItemId, Guid? excludeId, CancellationToken ct)
        {
            var currentDefaults = await _context.RecipeItemAlternatives
                .Where(a => a.RecipeItemId == recipeItemId && a.IsDefault && a.Id != excludeId)
                .ToListAsync(ct);

            foreach (var d in currentDefaults)
                d.IsDefault = false;
        }

        private static RecipeItemAlternativePricingType MapPricingType(int value)
        {
            return Enum.IsDefined(typeof(RecipeItemAlternativePricingType), value)
                ? (RecipeItemAlternativePricingType)value
                : RecipeItemAlternativePricingType.PriceDifference;
        }

        private static string? ValidatePricing(RecipeItemAlternativePricingType pricingType, decimal? fixedOverridePrice)
        {
            return pricingType == RecipeItemAlternativePricingType.FixedOverride &&
                (!fixedOverridePrice.HasValue || fixedOverridePrice.Value <= 0)
                ? "Fixed override alternatives require an override price."
                : null;
        }

        private static decimal? ResolveCustomerAdditionalPrice(
            RecipeItemAlternativePricingType pricingType,
            decimal? customerAdditionalPrice)
        {
            return pricingType == RecipeItemAlternativePricingType.PriceDifference
                ? customerAdditionalPrice
                : 0m;
        }

        private static decimal? ResolveFixedOverridePrice(
            RecipeItemAlternativePricingType pricingType,
            decimal? fixedOverridePrice)
        {
            return pricingType == RecipeItemAlternativePricingType.FixedOverride
                ? fixedOverridePrice
                : null;
        }

        internal static RecipeItemAlternativeDto MapToDto(RecipeItemAlternative a) => new()
        {
            Id = a.Id,
            RecipeItemId = a.RecipeItemId,
            RawMaterialId = a.RawMaterialId,
            RawMaterialName = a.RawMaterial?.Name ?? string.Empty,
            RawMaterialNameAr = a.RawMaterial?.NameAr,
            Name = a.Name,
            NameAr = a.NameAr,
            Amount = a.Amount,
            Unit = a.RawMaterial?.Unit.ToString() ?? string.Empty,
            PriceAdjustment = a.PriceAdjustment,
            AlternativePricingType = (int)a.PricingType,
            CustomerAdditionalPrice = a.CustomerAdditionalPrice,
            FixedOverridePrice = a.FixedOverridePrice,
            CustomerPriceImpact = ResolveCustomerPriceImpact(a),
            CostImpact = ResolveCostImpact(a),
            UnitCost = a.RawMaterial?.CostPerUnit ?? 0m,
            TotalCost = a.Amount * (a.RawMaterial?.CostPerUnit ?? 0m),
            IsDefault = a.IsDefault,
            IsActive = a.IsActive,
            SortOrder = a.SortOrder,
        };

        private static decimal ResolveCustomerPriceImpact(RecipeItemAlternative a)
        {
            return a.PricingType switch
            {
                RecipeItemAlternativePricingType.Included => 0m,
                RecipeItemAlternativePricingType.FixedOverride => ResolveFixedOverrideImpact(a),
                _ => a.CustomerAdditionalPrice ?? ResolveLegacyPriceImpact(a),
            };
        }

        private static decimal ResolveFixedOverrideImpact(RecipeItemAlternative a)
        {
            var basePrice = a.RecipeItem?.Product?.DiscountedPrice ?? a.RecipeItem?.Product?.BasePrice ?? 0m;
            return (a.FixedOverridePrice ?? a.PriceAdjustment) - basePrice;
        }

        private static decimal ResolveLegacyPriceImpact(RecipeItemAlternative a)
            => a.PriceAdjustment - ResolveOriginalRowCost(a);

        private static decimal ResolveCostImpact(RecipeItemAlternative a)
            => (a.Amount * (a.RawMaterial?.CostPerUnit ?? 0m)) - ResolveOriginalRowCost(a);

        private static decimal ResolveOriginalRowCost(RecipeItemAlternative a)
            => (a.RecipeItem?.Amount ?? 0m) * (a.RecipeItem?.RawMaterial?.CostPerUnit ?? 0m);
    }
}
