using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using RestaurantPos.Api.Options;
using RestaurantPos.Api.Services;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Modules.Retail.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly ITenantResolver _tenantResolver;
        private readonly PosDbContext _context; // Kept for import functionality temporarily
        private readonly IOptions<DeliveryPartnersOptions> _deliveryPartnersOptions;
        private readonly IRetailProductService _retailProducts;
        private const string DeliveryPartnersDisabledMessage = "Delivery partners feature is disabled.";
        private const int MaxProductPageSize = 100;

        public ProductsController(
            IProductService productService,
            ITenantResolver tenantResolver,
            PosDbContext context,
            IOptions<DeliveryPartnersOptions> deliveryPartnersOptions,
            IRetailProductService retailProducts)
        {
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _deliveryPartnersOptions = deliveryPartnersOptions ?? throw new ArgumentNullException(nameof(deliveryPartnersOptions));
            _retailProducts = retailProducts ?? throw new ArgumentNullException(nameof(retailProducts));
            _context = context; // For import functionality
        }

        // GET: api/Products
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts()
        {
            var tenantId = _tenantResolver.GetTenantId();
            var isArabic = IsArabicRequested(Request);
            var products = await _productService.GetAllProductsAsync(tenantId, isArabic);
            await _retailProducts.AttachRetailDetailsAsync(products, isArabic);
            return Ok(products);
        }

        [HttpGet("cashier")]
        [Authorize(Roles = AppRoleGroups.CashierOperators)]
        public async Task<ActionResult<IEnumerable<ProductDto>>> GetCashierProducts(
            [FromQuery] Guid? deliveryPartnerId,
            CancellationToken ct)
        {
            if (deliveryPartnerId.HasValue && !_deliveryPartnersOptions.Value.Enabled)
                return NotFound(new { message = DeliveryPartnersDisabledMessage });

            var tenantId = _tenantResolver.GetTenantId();
            var isArabic = IsArabicRequested(Request);
            var products = await _productService.GetCashierProductsAsync(tenantId, isArabic, deliveryPartnerId, ct);
            return Ok(products);
        }

        // GET: api/Products/public
        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ProductDto>>> GetPublicProducts()
        {
            var tenantId = _tenantResolver.GetTenantId();
            var isArabic = IsArabicRequested(Request);
            // Lean projection + per-locale cache — see ProductRepository.GetPublicActiveProductDtosAsync.
            // Drops internal cost/BOM fields that the showcase never renders; cuts JSON size
            // ~40–60 % on top of the response-compression savings configured in Program.cs.
            var products = await _productService.GetPublicActiveProductsAsync(tenantId, isArabic);
            return Ok(products);
        }

        // GET: api/Products/paginated?pageNumber=1&pageSize=10&search=...&categoryId=...&status=...&stationRouting=...
        [HttpGet("paginated")]
        public async Task<ActionResult<PaginatedResponse<ProductDto>>> GetProductsPaginated(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] Guid? categoryId = null,
            [FromQuery] string? status = null,
            [FromQuery] int? stationRouting = null,
            CancellationToken ct = default)
        {
            if (pageNumber < 1)
                return BadRequest(new { message = "Page number must be greater than zero." });
            if (pageSize < 1 || pageSize > MaxProductPageSize)
                return BadRequest(new { message = $"Page size must be between 1 and {MaxProductPageSize}." });

            var tenantId = _tenantResolver.GetTenantId();
            var isArabic = IsArabicRequested(Request);
            var page = await _productService.GetProductsPaginatedAsync(
                tenantId, isArabic, pageNumber, pageSize, search, categoryId, status, stationRouting, ct);
            await _retailProducts.AttachRetailDetailsAsync(page.Items, isArabic, ct);
            return Ok(page);
        }

        // Helper to avoid duplication
        private async Task<List<ProductDto>> FetchProductDtos()
        {
            var isArabic = IsArabicRequested(Request);

            return await _context.Products
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    NameAr = p.NameAr,
                    DisplayName = isArabic && p.NameAr != null && p.NameAr != string.Empty ? p.NameAr : p.Name,
                    Description = p.Description,
                    DescriptionAr = p.DescriptionAr,
                    Calories = p.Calories,
                    BasePrice = p.BasePrice,
                    CostPrice = p.CostPrice,
                    PricingMode = p.PricingMode,
                    Markup = p.Markup,
                    MarkupType = p.MarkupType,
                    DiscountPercentage = p.DiscountPercentage,
                    DiscountedPrice = p.DiscountedPrice,
                    CustomPrice = p.CustomPrice,
                    UseCustomPrice = p.UseCustomPrice,
                    DisplayPrice = p.UseCustomPrice && p.CustomPrice.HasValue
                        ? p.CustomPrice.Value
                        : (p.DiscountedPrice ?? p.BasePrice),
                    IsActive = p.IsActive,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    CategoryNameAr = p.Category != null ? p.Category.NameAr : null,
                    CategoryDisplayName = p.Category != null
                        ? (isArabic && p.Category.NameAr != null && p.Category.NameAr != string.Empty ? p.Category.NameAr : p.Category.Name)
                        : null,
                    SubcategoryId = p.SubcategoryId,
                    SubcategoryName = p.Subcategory != null ? p.Subcategory.Name : null,
                    SubcategoryNameAr = p.Subcategory != null ? p.Subcategory.NameAr : null,
                    SubcategoryDisplayName = p.Subcategory != null
                        ? (isArabic && p.Subcategory.NameAr != null && p.Subcategory.NameAr != string.Empty ? p.Subcategory.NameAr : p.Subcategory.Name)
                        : null,
                    Allergens = (int)p.Allergens,
                    StationRouting = (int)p.StationRouting,
                    PrinterIds = p.PrinterIds,
                    KitchenId = p.KitchenId,
                    KitchenName = p.Kitchen != null ? p.Kitchen.Name : null,
                    KitchenNameAr = p.Kitchen != null ? p.Kitchen.NameAr : null,
                    ImageUrl = p.ImageUrl,
                    IsAvailableNow = AvailabilityHelper.IsAvailableNow(
                        p.AvailableStartDate,
                        p.AvailableEndDate,
                        p.AvailableFrom,
                        p.AvailableTo),
                    AvailableStartDate = p.AvailableStartDate,
                    AvailableEndDate = p.AvailableEndDate,
                    AvailableFrom = p.AvailableFrom,
                    AvailableTo = p.AvailableTo,
                    ModifierGroups = p.ProductModifierGroups
                        .OrderBy(pmg => pmg.SortOrder)
                        .Select(pmg => new ModifierGroupDto
                        {
                            Id = pmg.ModifierGroup.Id,
                            Name = pmg.ModifierGroup.Name,
                            NameAr = pmg.ModifierGroup.NameAr,
                            DisplayName = isArabic && pmg.ModifierGroup.NameAr != null && pmg.ModifierGroup.NameAr != string.Empty
                                ? pmg.ModifierGroup.NameAr
                                : pmg.ModifierGroup.Name,
                            SelectionType = (int)pmg.ModifierGroup.SelectionType,
                            DisplayType = (int)pmg.ModifierGroup.DisplayType,
                            MinSelection = pmg.ModifierGroup.MinSelection,
                            MaxSelection = pmg.ModifierGroup.MaxSelection,
                            IsRequired = pmg.ModifierGroup.IsRequired,
                            AvailableForOrderTypes = (int)pmg.ModifierGroup.AvailableForOrderTypes,
                            PrintOnReceipt = pmg.ModifierGroup.PrintOnReceipt,
                            PrintInKitchen = pmg.ModifierGroup.PrintInKitchen,
                            IsShared = pmg.ModifierGroup.ProductGroups.Count > 1,
                            Modifiers = pmg.ModifierGroup.Modifiers
                                .Select(m => new ModifierDto
                                {
                                    Id = m.Id,
                                    Name = m.Name,
                                    NameAr = m.NameAr,
                                    DisplayName = isArabic && m.NameAr != null && m.NameAr != string.Empty ? m.NameAr : m.Name,
                                    PriceAdjustment = m.PriceAdjustment,
                                    PricingType = m.LinkedProductId.HasValue ? (int)PricingType.Fixed : (int)m.PricingType,
                                    IsFree = m.IsFree,
                                    FreeQuantityLimit = m.FreeQuantityLimit,
                                    MaxQuantity = m.MaxQuantity,
                                    LinkedRawMaterialId = m.LinkedRawMaterialId,
                                    LinkedProductId = m.LinkedProductId,
                                    LinkedMaterialAmount = m.LinkedMaterialAmount,
                                    IsDefault = m.IsDefault,
                                    IsActive = m.IsActive,
                                    RecipeItems = m.RecipeItems.Select(ri => new ModifierRecipeItemDto
                                    {
                                        Id = ri.Id,
                                        RawMaterialId = ri.RawMaterialId,
                                        RawMaterialName = ri.RawMaterial.Name,
                                        RawMaterialNameAr = ri.RawMaterial.NameAr,
                                        Amount = ri.Amount,
                                        Unit = ri.RawMaterial.Unit.ToString()
                                    }).ToList()
                                }).ToList()

                        }).ToList(),
                    RecipeItems = p.RecipeItems.Select(r => new RecipeItemDto
                    {
                        Id = r.Id,
                        RawMaterialId = r.RawMaterialId,
                        RawMaterialName = r.RawMaterial.Name,
                        RawMaterialNameAr = r.RawMaterial.NameAr,
                        ShowInMenu = r.RawMaterial.ShowInMenu,
                        IsPostPrice = r.RawMaterial.IsPostPrice,
                        Amount = r.Amount,
                        Unit = r.RawMaterial.Unit.ToString(),
                        UnitCost = r.RawMaterial.CostPerUnit,
                        TotalCost = r.Amount * r.RawMaterial.CostPerUnit
                    }).ToList(),
                    Options = p.Options
                        .Where(o => o.IsActive)
                        .OrderBy(o => o.SortOrder)
                        .ThenBy(o => o.Name)
                        .Select(o => new ProductOptionDto
                        {
                            Id = o.Id,
                            Name = o.Name,
                            NameAr = o.NameAr,
                            Price = o.Price,
                            IsDefault = o.IsDefault,
                            IsActive = o.IsActive,
                            SortOrder = o.SortOrder,
                            RecipeItems = o.RecipeItems.Select(ri => new ProductOptionRecipeItemDto
                            {
                                Id = ri.Id,
                                RawMaterialId = ri.RawMaterialId,
                                RawMaterialName = ri.RawMaterial.Name,
                                RawMaterialNameAr = ri.RawMaterial.NameAr,
                                ShowInMenu = ri.RawMaterial.ShowInMenu,
                                IsPostPrice = ri.RawMaterial.IsPostPrice,
                                Amount = ri.Amount,
                                Unit = ri.RawMaterial.Unit.ToString(),
                                UnitCost = ri.RawMaterial.CostPerUnit,
                                TotalCost = ri.Amount * ri.RawMaterial.CostPerUnit
                            }).ToList()
                        }).ToList()
                }).ToListAsync();
        }

        // POST: api/Products
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ProductDto>> CreateProduct(ProductCreateDto dto)
        {
            try
            {
                var tenantId = _tenantResolver.GetTenantId();
                dto.TenantId = tenantId;

                var product = await _productService.CreateProductAsync(dto, tenantId);
                await _retailProducts.UpsertAsync(product.Id, tenantId, dto.Retail);
                var isArabic = IsArabicRequested(Request);

                return CreatedAtAction(nameof(GetProducts), new { id = product.Id }, new ProductDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    NameAr = product.NameAr,
                    Description = product.Description,
                    DescriptionAr = product.DescriptionAr,
                    Calories = product.Calories,
                    IsAvailableNow = AvailabilityHelper.IsAvailableNow(
                        product.AvailableStartDate,
                        product.AvailableEndDate,
                        product.AvailableFrom,
                        product.AvailableTo),
                    AvailableStartDate = product.AvailableStartDate,
                    AvailableEndDate = product.AvailableEndDate,
                    AvailableFrom = product.AvailableFrom,
                    AvailableTo = product.AvailableTo,
                    CategoryId = product.CategoryId,
                    SubcategoryId = product.SubcategoryId,
                    DisplayName = ResolveDisplayName(product.Name, product.NameAr, isArabic)
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to create product", detail = ex.Message });
            }
        }

        // PUT: api/Products/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateProduct(Guid id, ProductCreateDto dto)
        {
            try
            {
                var tenantId = _tenantResolver.GetTenantId();
                dto.TenantId = tenantId;

                await _productService.UpdateProductAsync(id, dto, tenantId);
                await _retailProducts.UpsertAsync(id, tenantId, dto.Retail);
                return Ok(new { message = "Product updated successfully." });
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("not found"))
                    return NotFound(new { message = ex.Message });
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to update product", detail = ex.Message });
            }
        }

        // DELETE: api/Products/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            try
            {
                var tenantId = _tenantResolver.GetTenantId();
                await _productService.DeleteProductAsync(id, tenantId);
                return Ok(new { message = "Product deleted successfully." });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to delete product", detail = ex.Message });
            }
        }

            [HttpPost("{productId}/recipes")]
            [Authorize(Roles = "Admin")]
            public async Task<IActionResult> AddRecipeItem(Guid productId, [FromBody] RecipeItemCreateDto dto)
            {
                if (dto.Amount <= 0) 
                    return BadRequest(new { message = "Amount must be greater than zero." });

                try
                {
                    var tenantId = _tenantResolver.GetTenantId();
                    await _productService.AddRecipeItemAsync(productId, dto, tenantId);
                    return Ok(new { message = "Recipe item added successfully." });
                }
                catch (InvalidOperationException ex)
                {
                    return NotFound(new { message = ex.Message });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { message = "Server error", detail = ex.Message });
                }
            }
            [HttpPost("import")]
            [Authorize(Roles = "Admin")]
            public async Task<IActionResult> ImportProducts(IFormFile file)
            {
                if (file == null || file.Length == 0)
                    return BadRequest("Lütfen geçerli bir Excel dosyası yükleyin.");

                var tenantId = _tenantResolver.GetTenantId();
                int addedCount = 0;
                var newProducts = new List<Product>();

                try
                {
                    using (var stream = new MemoryStream())
                    {
                        await file.CopyToAsync(stream);
                        using (var workbook = new ClosedXML.Excel.XLWorkbook(stream))
                        {
                            var worksheet = workbook.Worksheet(1);
                            var rows = worksheet.RowsUsed().Skip(1); // Skip Header

                            foreach (var row in rows)
                            {
                                try
                                {
                                    // Columns: 1=Name, 2=Price, 3=Category, 4=Description (Optional)
                                    var name = row.Cell(1).GetValue<string>();
                                    if (string.IsNullOrWhiteSpace(name)) continue;

                                    var priceVal = row.Cell(2).GetValue<string>();
                                    if (!decimal.TryParse(priceVal, out var price)) price = 0;

                                    var categoryName = row.Cell(3).GetValue<string>();
                                    var description = row.Cell(4).GetValue<string>();

                                    var categoryId = Guid.Empty;
                                    if (!string.IsNullOrWhiteSpace(categoryName))
                                    {
                                        categoryId = GenerateGuidIdsFromString(categoryName);

                                        // Check if category exists, if not create locally to add to context
                                        var existingCategory = await _context.Categories.FindAsync(categoryId);
                                        if (existingCategory == null)
                                        {
                                            // Check if we already added it in this batch (local cache to avoid duplicates in loop)
                                            // Note: For simplicity in this fix, we just create it. 
                                            // EF Core ChangeTracker might catch duplicates if we keys match? 
                                            // Better to check context.ChangeTracker or just TryCatch or handle properly.
                                            // Safest quick way: 
                                            var localCategory = _context.ChangeTracker.Entries<Category>()
                                                .Select(e => e.Entity)
                                                .FirstOrDefault(c => c.Id == categoryId);

                                            if (localCategory == null)
                                            {
                                                var newCategory = new Category
                                                {
                                                    Id = categoryId,
                                                    Name = categoryName,
                                                    // TenantId handled by context or we set it if we have it?
                                                    // Assuming TenantResolver handles it or we set it explicitly if needed.
                                                    // For now, let's assume manual set isn't required if interceptor exists OR 
                                                    // we need to set it if BaseEntity requires it.
                                                    // BaseEntity in models has TenantId.
                                                    TenantId = tenantId
                                                };
                                                _context.Categories.Add(newCategory);
                                            }
                                        }
                                    }

                                    var product = new Product
                                    {
                                        Id = Guid.NewGuid(),
                                        TenantId = tenantId,
                                        Name = name,
                                        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                                        BasePrice = price,
                                        Markup = 1,
                                        MarkupType = "multiplier",
                                        DiscountPercentage = 0,
                                        DiscountedPrice = null,
                                        CostPrice = 0,
                                        IsActive = true,
                                        CategoryId = categoryId,
                                        // CreatedAt removed as it doesn't exist in model
                                    };

                                    newProducts.Add(product);
                                    addedCount++;
                                }
                                catch (Exception ex)
                                {
                                    // Log row error but continue
                                    continue;
                                }
                            }
                        }
                    }

                    if (newProducts.Any())
                    {
                        await _productService.BulkImportProductsAsync(newProducts, tenantId);
                    }

                    return Ok(new { count = addedCount, message = $"{addedCount} ürün başarıyla yüklendi." });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Dosya işlenirken hata oluştu: {ex.Message}");
                }
            }
        

        private Guid GenerateGuidIdsFromString(string input)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hash = md5.ComputeHash(System.Text.Encoding.Default.GetBytes(input));
                return new Guid(hash);
            }
        }

        private static bool IsArabicRequested(HttpRequest request)
        {
            var language = request.Headers["Accept-Language"].ToString();
            return language.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveDisplayName(string name, string? nameAr, bool isArabic)
        {
            return isArabic && !string.IsNullOrWhiteSpace(nameAr) ? nameAr : name;
        }
    }
}
