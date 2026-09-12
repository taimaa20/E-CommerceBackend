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
    public class ProductOptionsController : ControllerBase
    {
        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenantResolver;
        private readonly IBranchConfigurationService _branchConfigurationService;
        private readonly ILogger<ProductOptionsController> _logger;

        public ProductOptionsController(
            PosDbContext context,
            ITenantResolver tenantResolver,
            IBranchConfigurationService branchConfigurationService,
            ILogger<ProductOptionsController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _branchConfigurationService = branchConfigurationService ?? throw new ArgumentNullException(nameof(branchConfigurationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: api/ProductOptions/product/{productId}
        [HttpGet("product/{productId:guid}")]
        [Authorize]
        public async Task<ActionResult<List<ProductOptionDto>>> GetByProduct(Guid productId, CancellationToken ct)
        {
            var options = await _context.ProductOptions
                .Include(o => o.RecipeItems)
                    .ThenInclude(ri => ri.RawMaterial)
                .Where(o => o.ProductId == productId)
                .OrderBy(o => o.SortOrder)
                .ThenBy(o => o.Name)
                .ToListAsync(ct);

            return Ok(options.Select(MapToDto).ToList());
        }

        // POST: api/ProductOptions
        [HttpPost]
        public async Task<ActionResult<ProductOptionDto>> Create(
            [FromBody] ProductOptionCreateDto dto,
            CancellationToken ct)
        {
            var tenantId = _tenantResolver.GetTenantId();

            var productExists = await _context.Products
                .AnyAsync(p => p.Id == dto.ProductId, ct);
            if (!productExists)
                return NotFound("Product not found.");

            var option = new ProductOption
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductId = dto.ProductId,
                Name = dto.Name.Trim(),
                NameAr = dto.NameAr?.Trim(),
                Price = dto.Price,
                IsDefault = dto.IsDefault,
                IsActive = true,
                SortOrder = dto.SortOrder,
            };

            // Enforce single default per product
            if (dto.IsDefault)
                await ClearDefaultFlagAsync(dto.ProductId, null, ct);

            _context.ProductOptions.Add(option);
            await _context.SaveChangesAsync(ct);
            await _branchConfigurationService.EnsureMainBranchProductOptionsAsync(
                new[] { new MainBranchConfigurationSeed(option.TenantId, option.Id, option.SortOrder) },
                ct);

            _logger.LogInformation("ProductOption {OptionId} created for product {ProductId}", option.Id, dto.ProductId);

            return CreatedAtAction(nameof(GetByProduct), new { productId = dto.ProductId }, MapToDto(option));
        }

        // PUT: api/ProductOptions/{id}
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ProductOptionDto>> Update(
            Guid id,
            [FromBody] ProductOptionUpdateDto dto,
            CancellationToken ct)
        {
            var option = await _context.ProductOptions
                .Include(o => o.RecipeItems)
                    .ThenInclude(ri => ri.RawMaterial)
                .FirstOrDefaultAsync(o => o.Id == id, ct);

            if (option == null)
                return NotFound();

            if (dto.IsDefault && !option.IsDefault)
                await ClearDefaultFlagAsync(option.ProductId, id, ct);

            option.Name = dto.Name.Trim();
            option.NameAr = dto.NameAr?.Trim();
            option.Price = dto.Price;
            option.IsDefault = dto.IsDefault;
            option.IsActive = dto.IsActive;
            option.SortOrder = dto.SortOrder;

            await _context.SaveChangesAsync(ct);

            return Ok(MapToDto(option));
        }

        // DELETE: api/ProductOptions/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var option = await _context.ProductOptions.FindAsync(new object[] { id }, ct);
            if (option == null)
                return NotFound();

            await _branchConfigurationService.RemoveProductOptionBranchConfigurationsAsync(id, ct);
            _context.ProductOptions.Remove(option);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("ProductOption {OptionId} deleted", id);

            return NoContent();
        }

        // POST: api/ProductOptions/{optionId}/recipe
        [HttpPost("{optionId:guid}/recipe")]
        public async Task<ActionResult<ProductOptionRecipeItemDto>> AddRecipeItem(
            Guid optionId,
            [FromBody] ProductOptionRecipeItemCreateDto dto,
            CancellationToken ct)
        {
            if (dto.ProductOptionId != optionId)
                dto.ProductOptionId = optionId;

            var option = await _context.ProductOptions.FindAsync(new object[] { optionId }, ct);
            if (option == null)
                return NotFound("Option not found.");

            var material = await _context.RawMaterials.FindAsync(new object[] { dto.RawMaterialId }, ct);
            if (material == null)
                return NotFound("Raw material not found.");

            var item = new ProductOptionRecipeItem
            {
                Id = Guid.NewGuid(),
                TenantId = option.TenantId,
                ProductOptionId = optionId,
                RawMaterialId = dto.RawMaterialId,
                RawMaterial = material,
                Amount = dto.Amount,
            };

            _context.ProductOptionRecipeItems.Add(item);
            await _context.SaveChangesAsync(ct);

            return Ok(MapRecipeItemToDto(item));
        }

        // PUT: api/ProductOptions/recipe/{itemId}
        [HttpPut("recipe/{itemId:guid}")]
        public async Task<ActionResult<ProductOptionRecipeItemDto>> UpdateRecipeItem(
            Guid itemId,
            [FromBody] decimal amount,
            CancellationToken ct)
        {
            if (amount <= 0)
                return BadRequest("Amount must be greater than zero.");

            var item = await _context.ProductOptionRecipeItems
                .Include(ri => ri.RawMaterial)
                .FirstOrDefaultAsync(ri => ri.Id == itemId, ct);

            if (item == null)
                return NotFound();

            item.Amount = amount;
            await _context.SaveChangesAsync(ct);

            return Ok(MapRecipeItemToDto(item));
        }

        // DELETE: api/ProductOptions/recipe/{itemId}
        [HttpDelete("recipe/{itemId:guid}")]
        public async Task<IActionResult> DeleteRecipeItem(Guid itemId, CancellationToken ct)
        {
            var item = await _context.ProductOptionRecipeItems.FindAsync(new object[] { itemId }, ct);
            if (item == null)
                return NotFound();

            _context.ProductOptionRecipeItems.Remove(item);
            await _context.SaveChangesAsync(ct);

            return NoContent();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private async Task ClearDefaultFlagAsync(Guid productId, Guid? excludeOptionId, CancellationToken ct)
        {
            var currentDefaults = await _context.ProductOptions
                .Where(o => o.ProductId == productId && o.IsDefault && o.Id != excludeOptionId)
                .ToListAsync(ct);

            foreach (var opt in currentDefaults)
                opt.IsDefault = false;
        }

        private static ProductOptionDto MapToDto(ProductOption o) => new()
        {
            Id = o.Id,
            Name = o.Name,
            NameAr = o.NameAr,
            Price = o.Price,
            IsDefault = o.IsDefault,
            IsActive = o.IsActive,
            SortOrder = o.SortOrder,
            RecipeItems = (o.RecipeItems ?? Enumerable.Empty<ProductOptionRecipeItem>())
                .Select(MapRecipeItemToDto)
                .ToList(),
        };

        private static ProductOptionRecipeItemDto MapRecipeItemToDto(ProductOptionRecipeItem ri) => new()
        {
            Id = ri.Id,
            RawMaterialId = ri.RawMaterialId,
            RawMaterialName = ri.RawMaterial?.Name ?? string.Empty,
            RawMaterialNameAr = ri.RawMaterial?.NameAr,
            ShowInMenu = ri.RawMaterial?.ShowInMenu ?? false,
            IsPostPrice = ri.RawMaterial?.IsPostPrice ?? false,
            Amount = ri.Amount,
            Unit = ri.RawMaterial?.Unit.ToString() ?? string.Empty,
            UnitCost = ri.RawMaterial?.CostPerUnit ?? 0m,
            TotalCost = ri.Amount * (ri.RawMaterial?.CostPerUnit ?? 0m),
        };
    }
}
