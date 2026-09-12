using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services.Caching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    public interface IMenuService
    {
        /// Get complete public menu for a tenant
        Task<PublicMenuDto> GetPublicMenuAsync(string language = "en");

        /// Get categories for public display
        Task<List<PublicCategoryDto>> GetPublicCategoriesAsync(string language = "en");

        /// Get products for public display (filtered by active/available)
        Task<List<PublicProductDto>> GetPublicProductsAsync(string language = "en");

        /// Get products by category for public display
        Task<List<PublicProductDto>> GetPublicProductsByCategoryAsync(Guid categoryId, string language = "en");

        /// Get tenant branding for public menu
        Task<TenantBrandingDto> GetTenantBrandingAsync();

        /// Generate QR code token for tracking
        Task<string> GenerateQrCodeTokenAsync(string description = null);

        /// Get or create tenant menu settings
        Task<TenantMenuSettings> GetTenantMenuSettingsAsync();

        /// Update tenant menu settings
        Task<TenantMenuSettings> UpdateTenantMenuSettingsAsync(TenantMenuSettings settings);

        /// Check if menu access is allowed (rate limiting, etc.)
        Task<MenuAccessCheckResponseDto> CheckMenuAccessAsync(string clientIdentifier, string endpoint);

        /// Log menu access for audit trail
        Task LogMenuAccessAsync(string clientIpAddress, string userAgent, string accessType, string language);
    }

    public class MenuService : IMenuService
    {
        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenantResolver;
        private readonly ITenantService _tenantService;
        private readonly ICacheService _cache;
        private readonly IServiceScopeFactory _scopeFactory;
        private const int RATE_LIMIT_REQUESTS_PER_MINUTE = 60; // 60 requests per minute per IP

        public MenuService(
            PosDbContext context,
            ITenantResolver tenantResolver,
            ITenantService tenantService,
            ICacheService cache,
            IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _tenantResolver = tenantResolver;
            _tenantService = tenantService;
            _cache = cache;
            _scopeFactory = scopeFactory;
        }

        // ─── PUBLIC MENU ACCESS ────────────────────────────────────

        public async Task<PublicMenuDto> GetPublicMenuAsync(string language = "en")
        {
            var tenantId = _tenantResolver.GetTenantId();
            var cacheKey = $"pos:menu:public:{tenantId}:{language}";

            return await _cache.GetOrCreateAsync(
                cacheKey,
                async () =>
                {
                    var isArabic = language.ToLower() == "ar";
                    var categories = await GetPublicCategoriesInternalAsync(language);
                    var products = await GetPublicProductsInternalAsync(language);
                    var tenant = await _context.Tenants.FindAsync(tenantId);

                    return new PublicMenuDto
                    {
                        TenantId = tenantId,
                        TenantName = tenant?.Name ?? "Store",
                        GeneratedAt = DateTime.UtcNow,
                        Categories = categories,
                        Products = products
                    };
                },
                slidingExpiration: TimeSpan.FromMinutes(30),
                absoluteExpiration: TimeSpan.FromHours(2)
            );
        }

        public async Task<List<PublicCategoryDto>> GetPublicCategoriesAsync(string language = "en")
        {
            var tenantId = _tenantResolver.GetTenantId();
            var cacheKey = $"pos:menu:categories:{tenantId}:{language}";

            return await _cache.GetOrCreateAsync(
                cacheKey,
                async () => await GetPublicCategoriesInternalAsync(language),
                slidingExpiration: TimeSpan.FromMinutes(30),
                absoluteExpiration: TimeSpan.FromHours(2)
            );
        }

        public async Task<List<PublicProductDto>> GetPublicProductsAsync(string language = "en")
        {
            var tenantId = _tenantResolver.GetTenantId();
            var cacheKey = $"pos:menu:products:{tenantId}:{language}";

            return await _cache.GetOrCreateAsync(
                cacheKey,
                async () => await GetPublicProductsInternalAsync(language),
                slidingExpiration: TimeSpan.FromMinutes(30),
                absoluteExpiration: TimeSpan.FromHours(2)
            );
        }

        // Internal methods (without caching)
        private async Task<List<PublicCategoryDto>> GetPublicCategoriesInternalAsync(string language = "en")
        {
            var isArabic = language.ToLower() == "ar";

            return await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new PublicCategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    NameAr = c.NameAr,
                    DisplayName = isArabic && !string.IsNullOrEmpty(c.NameAr) ? c.NameAr : c.Name,
                    Description = c.Description,
                    ImageUrl = c.ImageUrl,
                    SortOrder = c.SortOrder,
                    IsActive = c.IsActive,
                    Subcategories = c.Subcategories
                        .Where(s => s.IsActive)
                        .OrderBy(s => s.DisplayOrder)
                        .ThenBy(s => s.Name)
                        .Select(s => new PublicSubcategoryDto
                        {
                            Id = s.Id,
                            CategoryId = s.CategoryId,
                            Name = s.Name,
                            NameAr = s.NameAr,
                            DisplayName = isArabic && !string.IsNullOrEmpty(s.NameAr) ? s.NameAr : s.Name,
                            DisplayOrder = s.DisplayOrder,
                            IsActive = s.IsActive
                        })
                        .ToList()
                })
                .ToListAsync();
        }

        private async Task<List<PublicProductDto>> GetPublicProductsInternalAsync(string language = "en")
        {
            return await MapProductsToPublicDto(
                _context.Products.Where(p => p.IsActive),
                language
            );
        }

        public async Task<List<PublicProductDto>> GetPublicProductsByCategoryAsync(Guid categoryId, string language = "en")
        {
            return await MapProductsToPublicDto(
                _context.Products.Where(p => p.IsActive && p.CategoryId == categoryId),
                language
            );
        }

        // ─── PRIVATE HELPERS ──────────────────────────────────────

        private async Task<List<PublicProductDto>> MapProductsToPublicDto(IQueryable<Product> query, string language)
        {
            var isArabic = language.ToLower() == "ar";
            var settings = await GetTenantMenuSettingsAsync();

            return await query
                .Select(p => new PublicProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    NameAr = p.NameAr,
                    DisplayName = isArabic && !string.IsNullOrEmpty(p.NameAr) ? p.NameAr : p.Name,
                    Description = isArabic && !string.IsNullOrEmpty(p.DescriptionAr) ? p.DescriptionAr : p.Description,
                    DescriptionAr = p.DescriptionAr,
                    Calories = p.Calories,
                    
                    // Pricing (public only shows final price to customer).
                    // Promotional CustomPrice wins when toggled on; otherwise
                    // fall back to existing DiscountedPrice → BasePrice chain.
                    // POS / orders are unaffected — they read BasePrice directly.
                    DisplayPrice = p.UseCustomPrice && p.CustomPrice.HasValue
                        ? p.CustomPrice.Value
                        : (p.DiscountedPrice ?? p.BasePrice),
                    BasePrice = p.BasePrice,
                    OriginalPrice = (p.UseCustomPrice && p.CustomPrice.HasValue) || p.DiscountedPrice.HasValue
                        ? p.BasePrice
                        : (decimal?)null,
                    DiscountPercentage = p.UseCustomPrice && p.CustomPrice.HasValue
                        ? null
                        : p.DiscountPercentage,
                    
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    CategoryDisplayName = p.Category != null 
                        ? (isArabic && !string.IsNullOrEmpty(p.Category.NameAr) ? p.Category.NameAr : p.Category.Name)
                        : null,
                    SubcategoryId = p.Subcategory != null && p.Subcategory.IsActive ? p.SubcategoryId : null,
                    SubcategoryName = p.Subcategory != null && p.Subcategory.IsActive ? p.Subcategory.Name : null,
                    SubcategoryNameAr = p.Subcategory != null && p.Subcategory.IsActive ? p.Subcategory.NameAr : null,
                    SubcategoryDisplayName = p.Subcategory != null && p.Subcategory.IsActive
                        ? (isArabic && !string.IsNullOrEmpty(p.Subcategory.NameAr) ? p.Subcategory.NameAr : p.Subcategory.Name)
                        : null,
                    
                    ImageUrl = p.ImageUrl ?? settings.DefaultProductImageUrl,
                    ImageKey = p.ImageKey,
                    Allergens = (int)p.Allergens,
                    IsAvailable = p.IsActive,
                    IsSoon = p.IsSoon,
                    
                    ModifierGroups = p.ProductModifierGroups
                        .OrderBy(pmg => pmg.SortOrder)
                        .Select(pmg => new PublicModifierGroupDto
                        {
                            Id = pmg.ModifierGroup.Id,
                            Name = pmg.ModifierGroup.Name,
                            NameAr = pmg.ModifierGroup.NameAr,
                            DisplayName = isArabic && !string.IsNullOrEmpty(pmg.ModifierGroup.NameAr)
                                ? pmg.ModifierGroup.NameAr
                                : pmg.ModifierGroup.Name,
                            SelectionType = (int)pmg.ModifierGroup.SelectionType,
                            MinSelection = pmg.ModifierGroup.MinSelection,
                            MaxSelection = pmg.ModifierGroup.MaxSelection,
                            Modifiers = pmg.ModifierGroup.Modifiers
                                .Where(m => m.IsActive)
                                .Select(m => new PublicModifierDto
                                {
                                    Id = m.Id,
                                    Name = m.Name,
                                    NameAr = m.NameAr,
                                    DisplayName = isArabic && !string.IsNullOrEmpty(m.NameAr) ? m.NameAr : m.Name,
                                    PriceAdjustment = m.PriceAdjustment,
                                    IsDefault = m.IsDefault
                                })
                                .ToList()
                        })
                        .ToList(),
                    
                    // Ingredients hidden for promo-only products: when an admin
                    // turns on UseCustomPrice we treat the item as a marketing
                    // listing rather than a costed recipe, so the website / menu
                    // never surfaces stale or placeholder ingredient rows.
                    Ingredients = settings.ShowIngredientsBreakdown && !p.UseCustomPrice
                        ? p.RecipeItems
                            .Where(ri => ri.RawMaterial.ShowInMenu)
                            .Select(ri => new IngredientSummaryDto
                            {
                                Name = ri.RawMaterial.Name,
                                NameAr = ri.RawMaterial.NameAr,
                                DisplayName = isArabic && !string.IsNullOrEmpty(ri.RawMaterial.NameAr)
                                    ? ri.RawMaterial.NameAr
                                    : ri.RawMaterial.Name
                            })
                            .ToList()
                        : null
                })
                .ToListAsync();
        }

        // ─── TENANT BRANDING ───────────────────────────────────────

        public async Task<TenantBrandingDto> GetTenantBrandingAsync()
        {
            var tenantId = _tenantResolver.GetTenantId();
            var tenant = await _context.Tenants.FindAsync(tenantId);
            if (tenant == null)
                throw new InvalidOperationException("Tenant not found");

            var settings = await GetTenantMenuSettingsAsync();

            return new TenantBrandingDto
            {
                TenantId = tenant.Id,
                SiteName = tenant.BusinessName ?? tenant.Name,
                Description = settings.MenuDescription,
                LogoUrl = tenant.LogoUrl,
                BannerUrl = settings.HeroBannerUrl,
                PrimaryColor = tenant.PrimaryColor ?? "#f97316",
                AccentColor = tenant.AccentColor,
                PhoneNumber = null, // Can be added to tenant model later
                Address = null, // Can be added to tenant model later
                AllowQrAccess = settings.EnableQrAccess,
                RequireLanguageSelection = settings.RequireLanguageSelection
            };
        }

        // ─── QR CODE MANAGEMENT ────────────────────────────────────

        public async Task<string> GenerateQrCodeTokenAsync(string description = null)
        {
            var tenantId = _tenantResolver.GetTenantId();
            var token = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();

            var qrAccess = new QrCodeAccess
            {
                TenantId = tenantId,
                QrCodeToken = token,
                Description = description ?? "Generated QR Code",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.QrCodeAccess.Add(qrAccess);
            await _context.SaveChangesAsync();

            return token;
        }

        // ─── TENANT SETTINGS ────────────────────────────────────────

        public async Task<TenantMenuSettings> GetTenantMenuSettingsAsync()
        {
            var tenantId = _tenantResolver.GetTenantId();

            var settings = await _context.TenantMenuSettings
                .Where(tms => tms.TenantId == tenantId && tms.IsActive)
                .FirstOrDefaultAsync();

            if (settings == null)
            {
                // Create default settings if none exist
                settings = new TenantMenuSettings
                {
                    TenantId = tenantId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.TenantMenuSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            return settings;
        }

        public async Task<TenantMenuSettings> UpdateTenantMenuSettingsAsync(TenantMenuSettings settings)
        {
            var tenantId = _tenantResolver.GetTenantId();
            
            if (settings.TenantId != tenantId)
                throw new UnauthorizedAccessException("Cannot update settings for different tenant");

            settings.UpdatedAt = DateTime.UtcNow;
            _context.TenantMenuSettings.Update(settings);
            await _context.SaveChangesAsync();

            return settings;
        }

        // ─── RATE LIMITING ─────────────────────────────────────────

        // Per-IP/endpoint counter held in the in-process cache.
        // WHY: the previous DB-backed implementation referenced RateLimitEntry,
        // which was never registered in PosDbContext, so /menu/public crashed
        // on every call ("Cannot create a DbSet for 'RateLimitEntry'..."). The
        // original inline note already pointed at MemoryCache as the intended
        // production path — a per-request rate counter doesn't need durability
        // and writing it to PostgreSQL on every public hit was wasteful anyway.
        private sealed class RateLimitState
        {
            public int RequestCount { get; set; }
            public DateTime WindowStartTime { get; set; }
        }

        public async Task<MenuAccessCheckResponseDto> CheckMenuAccessAsync(string clientIdentifier, string endpoint)
        {
            if (string.IsNullOrWhiteSpace(clientIdentifier) || string.IsNullOrWhiteSpace(endpoint))
                return new MenuAccessCheckResponseDto
                {
                    IsAllowed = false,
                    Message = "Invalid parameters"
                };

            var cacheKey = $"pos:ratelimit:{clientIdentifier}:{endpoint}";
            var now = DateTime.UtcNow;

            var state = await _cache.GetAsync<RateLimitState>(cacheKey);

            if (state != null && (now - state.WindowStartTime).TotalMinutes < 1)
            {
                if (state.RequestCount >= RATE_LIMIT_REQUESTS_PER_MINUTE)
                {
                    return new MenuAccessCheckResponseDto
                    {
                        IsAllowed = false,
                        RemainingRequests = 0,
                        ResetSeconds = (int)Math.Ceiling((60 - (now - state.WindowStartTime).TotalSeconds)),
                        Message = "Rate limit exceeded"
                    };
                }

                state.RequestCount++;
            }
            else
            {
                state = new RateLimitState
                {
                    RequestCount = 1,
                    WindowStartTime = now,
                };
            }

            // Absolute expiration of one minute matches the rate-limit window —
            // entries auto-expire so we never leak per-IP state.
            await _cache.SetAsync(cacheKey, state, absoluteExpiration: TimeSpan.FromMinutes(1));

            return new MenuAccessCheckResponseDto
            {
                IsAllowed = true,
                RemainingRequests = RATE_LIMIT_REQUESTS_PER_MINUTE - state.RequestCount,
                ResetSeconds = (int)Math.Ceiling((60 - (now - state.WindowStartTime).TotalSeconds)),
                Message = "Access allowed"
            };
        }

        // ─── AUDIT LOGGING ─────────────────────────────────────────

        public Task LogMenuAccessAsync(string clientIpAddress, string userAgent, string accessType, string language)
        {
            // Resolve the tenant on the inbound request thread — the scoped
            // resolver is bound to the current HttpContext and goes away the
            // moment the response flushes. Snapshot every value we need before
            // handing the work off to the background task.
            var tenantId = _tenantResolver.GetTenantId();

            var log = new MenuAccessLog
            {
                TenantId = tenantId,
                ClientIpAddress = clientIpAddress,
                UserAgent = userAgent,
                AccessType = accessType,
                Language = language,
                AccessedAt = DateTime.UtcNow,
                Success = true
            };

            // Fire-and-forget. The audit row is non-critical for the caller's
            // response (it shaved ~100ms+ per /menu/public hit because every
            // public-menu request was synchronously awaiting a Postgres INSERT
            // before returning the cached payload). PosDbContext is request-
            // scoped and disposed as soon as we return Ok(menu), so we open a
            // brand-new DI scope here and resolve a fresh DbContext that owns
            // its own connection lifetime independent of the inbound request.
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
                    db.MenuAccessLogs.Add(log);
                    await db.SaveChangesAsync();
                }
                catch
                {
                    // Best-effort audit. Swallow so an unobserved task
                    // exception can't tear down the host process.
                }
            });

            return Task.CompletedTask;
        }
    }
}
