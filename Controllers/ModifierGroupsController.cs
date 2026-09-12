using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;
using Microsoft.AspNetCore.Authorization;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = AppRoleGroups.AdminOnly)]
    public class ModifierGroupsController : ControllerBase
    {
        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenantResolver;
        private readonly IBranchConfigurationService _branchConfigurationService;

        public ModifierGroupsController(
            PosDbContext context,
            ITenantResolver tenantResolver,
            IBranchConfigurationService branchConfigurationService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _branchConfigurationService = branchConfigurationService ?? throw new ArgumentNullException(nameof(branchConfigurationService));
        }

        // GET: api/ModifierGroups
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ModifierGroupLibraryDto>>> GetAll()
        {
            var groups = await _context.ModifierGroups
                .Include(mg => mg.Modifiers)
                .Include(mg => mg.ProductGroups)
                    .ThenInclude(pg => pg.Product)
                .OrderBy(mg => mg.Name)
                .Select(mg => new ModifierGroupLibraryDto
                {
                    Id = mg.Id,
                    Name = mg.Name,
                    NameAr = mg.NameAr,
                    SelectionType = (int)mg.SelectionType,
                    DisplayType = (int)mg.DisplayType,
                    MinSelection = mg.MinSelection,
                    MaxSelection = mg.MaxSelection,
                    IsRequired = mg.IsRequired,
                    AvailableForOrderTypes = (int)mg.AvailableForOrderTypes,
                    PrintOnReceipt = mg.PrintOnReceipt,
                    PrintInKitchen = mg.PrintInKitchen,
                    Modifiers = mg.Modifiers.Select(m => new ModifierDto
                    {
                        Id = m.Id,
                        Name = m.Name,
                        NameAr = m.NameAr,
                        DisplayName = m.Name,
                        PricingType = m.LinkedProductId.HasValue ? (int)PricingType.Fixed : (int)m.PricingType,
                        PriceAdjustment = m.PriceAdjustment,
                        IsFree = m.IsFree,
                        FreeQuantityLimit = m.FreeQuantityLimit,
                        IsDefault = m.IsDefault,
                        IsActive = m.IsActive,
                        MaxQuantity = m.MaxQuantity,
                        LinkedRawMaterialId = m.LinkedRawMaterialId,
                        LinkedProductId = m.LinkedProductId,
                        LinkedMaterialAmount = m.LinkedMaterialAmount
                    }).ToList(),
                    UsedByProducts = mg.ProductGroups.Select(pg => new ModifierGroupProductRefDto
                    {
                        ProductId = pg.ProductId,
                        ProductName = pg.Product.Name
                    }).ToList()
                })
                .ToListAsync();

            return Ok(groups);
        }

        // GET: api/ModifierGroups/paginated?pageNumber=1&pageSize=10&search=...
        [HttpGet("paginated")]
        public async Task<ActionResult<PaginatedResponse<ModifierGroupLibraryDto>>> GetAllPaginated(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            var query = _context.ModifierGroups.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(mg =>
                    EF.Functions.ILike(mg.Name, $"%{s}%") ||
                    (mg.NameAr != null && EF.Functions.ILike(mg.NameAr, $"%{s}%")));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(mg => mg.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Include(mg => mg.Modifiers)
                .Include(mg => mg.ProductGroups)
                    .ThenInclude(pg => pg.Product)
                .Select(mg => new ModifierGroupLibraryDto
                {
                    Id = mg.Id,
                    Name = mg.Name,
                    NameAr = mg.NameAr,
                    SelectionType = (int)mg.SelectionType,
                    DisplayType = (int)mg.DisplayType,
                    MinSelection = mg.MinSelection,
                    MaxSelection = mg.MaxSelection,
                    IsRequired = mg.IsRequired,
                    AvailableForOrderTypes = (int)mg.AvailableForOrderTypes,
                    PrintOnReceipt = mg.PrintOnReceipt,
                    PrintInKitchen = mg.PrintInKitchen,
                    Modifiers = mg.Modifiers.Select(m => new ModifierDto
                    {
                        Id = m.Id,
                        Name = m.Name,
                        NameAr = m.NameAr,
                        DisplayName = m.Name,
                        PricingType = m.LinkedProductId.HasValue ? (int)PricingType.Fixed : (int)m.PricingType,
                        PriceAdjustment = m.PriceAdjustment,
                        IsFree = m.IsFree,
                        FreeQuantityLimit = m.FreeQuantityLimit,
                        IsDefault = m.IsDefault,
                        IsActive = m.IsActive,
                        MaxQuantity = m.MaxQuantity,
                        LinkedRawMaterialId = m.LinkedRawMaterialId,
                        LinkedProductId = m.LinkedProductId,
                        LinkedMaterialAmount = m.LinkedMaterialAmount
                    }).ToList(),
                    UsedByProducts = mg.ProductGroups.Select(pg => new ModifierGroupProductRefDto
                    {
                        ProductId = pg.ProductId,
                        ProductName = pg.Product.Name
                    }).ToList()
                })
                .ToListAsync();

            return Ok(new PaginatedResponse<ModifierGroupLibraryDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }

        // GET: api/ModifierGroups/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ModifierGroupLibraryDto>> GetById(Guid id)
        {
            var mg = await _context.ModifierGroups
                .Include(g => g.Modifiers)
                .Include(g => g.ProductGroups)
                    .ThenInclude(pg => pg.Product)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (mg == null)
                return NotFound(new { message = "Modifier group not found." });

            return Ok(new ModifierGroupLibraryDto
            {
                Id = mg.Id,
                Name = mg.Name,
                NameAr = mg.NameAr,
                SelectionType = (int)mg.SelectionType,
                DisplayType = (int)mg.DisplayType,
                MinSelection = mg.MinSelection,
                MaxSelection = mg.MaxSelection,
                IsRequired = mg.IsRequired,
                AvailableForOrderTypes = (int)mg.AvailableForOrderTypes,
                PrintOnReceipt = mg.PrintOnReceipt,
                PrintInKitchen = mg.PrintInKitchen,
                Modifiers = mg.Modifiers.Select(m => new ModifierDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    NameAr = m.NameAr,
                    DisplayName = m.Name,
                    PricingType = m.LinkedProductId.HasValue ? (int)PricingType.Fixed : (int)m.PricingType,
                    PriceAdjustment = m.PriceAdjustment,
                    IsFree = m.IsFree,
                    FreeQuantityLimit = m.FreeQuantityLimit,
                    IsDefault = m.IsDefault,
                    IsActive = m.IsActive,
                    MaxQuantity = m.MaxQuantity,
                    LinkedRawMaterialId = m.LinkedRawMaterialId,
                    LinkedProductId = m.LinkedProductId,
                    LinkedMaterialAmount = m.LinkedMaterialAmount
                }).ToList(),
                UsedByProducts = mg.ProductGroups.Select(pg => new ModifierGroupProductRefDto
                {
                    ProductId = pg.ProductId,
                    ProductName = pg.Product.Name
                }).ToList()
            });
        }

        // POST: api/ModifierGroups
        [HttpPost]
        public async Task<ActionResult<ModifierGroupLibraryDto>> Create(
            ModifierGroupCreateDto dto,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "Group name is required." });

            var linkedProductPrices = await LoadLinkedProductPricesAsync(dto.Modifiers, ct);
            if (linkedProductPrices.Error != null)
                return BadRequest(new { message = linkedProductPrices.Error });

            var tenantId = _tenantResolver.GetTenantId();

            var groupId = Guid.NewGuid();
            var group = new ModifierGroup
            {
                Id = groupId,
                Name = dto.Name.Trim(),
                NameAr = dto.NameAr?.Trim(),
                SelectionType = (SelectionType)dto.SelectionType,
                DisplayType = (DisplayType)dto.DisplayType,
                MinSelection = dto.MinSelection,
                MaxSelection = dto.MaxSelection,
                IsRequired = dto.IsRequired,
                AvailableForOrderTypes = (OrderTypeFlag)dto.AvailableForOrderTypes,
                PrintOnReceipt = dto.PrintOnReceipt,
                PrintInKitchen = dto.PrintInKitchen,
                TenantId = tenantId,
                Modifiers = dto.Modifiers
                    .Select(m => CreateModifier(m, tenantId, groupId, linkedProductPrices.Prices))
                    .ToList()
            };

            _context.ModifierGroups.Add(group);
            await _context.SaveChangesAsync(ct);
            await _branchConfigurationService.EnsureMainBranchModifierGroupsAsync(
                new[] { new MainBranchConfigurationSeed(group.TenantId, group.Id) },
                ct);
            await _branchConfigurationService.EnsureMainBranchModifiersAsync(
                group.Modifiers.Select(modifier => new MainBranchConfigurationSeed(modifier.TenantId, modifier.Id)).ToList(),
                ct);

            return CreatedAtAction(nameof(GetById), new { id = group.Id }, new ModifierGroupLibraryDto
            {
                Id = group.Id,
                Name = group.Name,
                NameAr = group.NameAr,
                SelectionType = (int)group.SelectionType,
                DisplayType = (int)group.DisplayType,
                MinSelection = group.MinSelection,
                MaxSelection = group.MaxSelection,
                IsRequired = group.IsRequired,
                AvailableForOrderTypes = (int)group.AvailableForOrderTypes,
                PrintOnReceipt = group.PrintOnReceipt,
                PrintInKitchen = group.PrintInKitchen,
                Modifiers = group.Modifiers.Select(m => new ModifierDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    NameAr = m.NameAr,
                    DisplayName = m.Name,
                    PricingType = m.LinkedProductId.HasValue ? (int)PricingType.Fixed : (int)m.PricingType,
                    PriceAdjustment = m.PriceAdjustment,
                    IsFree = m.IsFree,
                    FreeQuantityLimit = m.FreeQuantityLimit,
                    IsDefault = m.IsDefault,
                    IsActive = m.IsActive,
                    MaxQuantity = m.MaxQuantity,
                    LinkedRawMaterialId = m.LinkedRawMaterialId,
                    LinkedProductId = m.LinkedProductId,
                    LinkedMaterialAmount = m.LinkedMaterialAmount
                }).ToList(),
                UsedByProducts = new List<ModifierGroupProductRefDto>()
            });
        }

        // PUT: api/ModifierGroups/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(
            Guid id,
            ModifierGroupCreateDto dto,
            CancellationToken ct = default)
        {
            var group = await _context.ModifierGroups
                .Include(g => g.Modifiers)
                .FirstOrDefaultAsync(g => g.Id == id, ct);

            if (group == null)
                return NotFound(new { message = "Modifier group not found." });

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "Group name is required." });

            var linkedProductPrices = await LoadLinkedProductPricesAsync(dto.Modifiers, ct);
            if (linkedProductPrices.Error != null)
                return BadRequest(new { message = linkedProductPrices.Error });

            group.Name = dto.Name.Trim();
            group.NameAr = dto.NameAr?.Trim();
            group.SelectionType = (SelectionType)dto.SelectionType;
            group.DisplayType = (DisplayType)dto.DisplayType;
            group.MinSelection = dto.MinSelection;
            group.MaxSelection = dto.MaxSelection;
            group.IsRequired = dto.IsRequired;
            group.AvailableForOrderTypes = (OrderTypeFlag)dto.AvailableForOrderTypes;
            group.PrintOnReceipt = dto.PrintOnReceipt;
            group.PrintInKitchen = dto.PrintInKitchen;

            // Sync modifiers: remove old, add new
            var oldModifiers = group.Modifiers.ToList();
            await _branchConfigurationService.RemoveModifierBranchConfigurationsAsync(
                oldModifiers.Select(m => m.Id).ToList(),
                ct);
            _context.Modifiers.RemoveRange(oldModifiers);

            var newModifiers = dto.Modifiers
                .Select(m => CreateModifier(m, group.TenantId, group.Id, linkedProductPrices.Prices))
                .ToList();
            _context.Modifiers.AddRange(newModifiers);

            await _context.SaveChangesAsync(ct);
            await _branchConfigurationService.EnsureMainBranchModifiersAsync(
                newModifiers.Select(modifier => new MainBranchConfigurationSeed(modifier.TenantId, modifier.Id)).ToList(),
                ct);

            return Ok(new { message = "Modifier group updated." });
        }

        private async Task<(Dictionary<Guid, decimal> Prices, string? Error)> LoadLinkedProductPricesAsync(
            IEnumerable<ModifierCreateDto> modifiers,
            CancellationToken ct)
        {
            var linkedProductIds = modifiers
                .Select(m => m.LinkedProductId)
                .Where(id => id.HasValue && id.Value != Guid.Empty)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            if (linkedProductIds.Count == 0)
                return (new Dictionary<Guid, decimal>(), null);

            var prices = await _context.Products
                .AsNoTracking()
                .Where(p => linkedProductIds.Contains(p.Id))
                .Select(p => new { p.Id, Price = p.DiscountedPrice ?? p.BasePrice })
                .ToDictionaryAsync(p => p.Id, p => p.Price, ct);

            return prices.Count == linkedProductIds.Count
                ? (prices, null)
                : (prices, "One or more linked products do not exist.");
        }

        private static Modifier CreateModifier(
            ModifierCreateDto dto,
            Guid tenantId,
            Guid? modifierGroupId,
            IReadOnlyDictionary<Guid, decimal> linkedProductPrices)
        {
            var linkedProductId = dto.LinkedProductId is { } productId && productId != Guid.Empty
                ? productId
                : (Guid?)null;

            var modifier = new Modifier
            {
                Id = Guid.NewGuid(),
                Name = dto.Name.Trim(),
                NameAr = dto.NameAr?.Trim(),
                PricingType = linkedProductId.HasValue ? PricingType.Fixed : (PricingType)dto.PricingType,
                PriceAdjustment = linkedProductId.HasValue
                    ? linkedProductPrices[linkedProductId.Value]
                    : dto.PriceAdjustment,
                IsFree = dto.IsFree,
                FreeQuantityLimit = dto.FreeQuantityLimit,
                IsDefault = dto.IsDefault,
                IsActive = dto.IsActive,
                MaxQuantity = dto.MaxQuantity,
                LinkedRawMaterialId = linkedProductId.HasValue ? null : dto.LinkedRawMaterialId,
                LinkedProductId = linkedProductId,
                LinkedMaterialAmount = linkedProductId.HasValue ? 0 : dto.LinkedMaterialAmount,
                TenantId = tenantId
            };

            if (modifierGroupId.HasValue)
                modifier.ModifierGroupId = modifierGroupId.Value;

            return modifier;
        }

        // DELETE: api/ModifierGroups/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        {
            var group = await _context.ModifierGroups
                .Include(g => g.Modifiers)
                .Include(g => g.ProductGroups)
                .FirstOrDefaultAsync(g => g.Id == id, ct);

            if (group == null)
                return NotFound(new { message = "Modifier group not found." });

            if (group.ProductGroups.Any())
                return Conflict(new { message = $"Cannot delete: group is used by {group.ProductGroups.Count} product(s). Unlink it from all products first." });

            await _branchConfigurationService.RemoveModifierGroupBranchConfigurationsAsync(new[] { group.Id }, ct);
            await _branchConfigurationService.RemoveModifierBranchConfigurationsAsync(
                group.Modifiers.Select(m => m.Id).ToList(),
                ct);
            _context.Modifiers.RemoveRange(group.Modifiers);
            _context.ModifierGroups.Remove(group);
            await _context.SaveChangesAsync(ct);

            return Ok(new { message = "Modifier group deleted." });
        }
    }
}
