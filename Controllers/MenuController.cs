using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Services;
using System;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MenuController : ControllerBase
    {
        private readonly IMenuService _menuService;
        private readonly ITenantResolver _tenantResolver;

        public MenuController(IMenuService menuService, ITenantResolver tenantResolver)
        {
            _menuService = menuService;
            _tenantResolver = tenantResolver;
        }

        // ─── PUBLIC ENDPOINTS (AllowAnonymous) ──────────────────

        // Browser/CDN caching for the public marketing surface.
        // 30s fresh + 120s stale-while-revalidate gives near-zero perceived
        // load on repeat visits while keeping admin edits visible within 30s.
        // Backend's own ICacheService cache (30 min) is invalidated on product
        // mutations via ProductService.RemoveByPatternAsync("pos:menu:*").
        private const string PublicMenuCacheControl = "public, max-age=30, stale-while-revalidate=120";

        /// <summary>
        /// Get complete public menu (categories + products)
        /// Public access - no authentication required
        /// </summary>
        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<ActionResult<PublicMenuDto>> GetPublicMenu([FromQuery] string language = "en")
        {
            try
            {
                // Get client IP for rate limiting
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                // Check rate limit
                var accessCheck = await _menuService.CheckMenuAccessAsync(clientIp, "/api/menu/public");
                if (!accessCheck.IsAllowed)
                    return StatusCode(429, new { message = "Too many requests", resetIn = accessCheck.ResetSeconds });

                // Get menu
                var menu = await _menuService.GetPublicMenuAsync(language);

                // Log access
                await _menuService.LogMenuAccessAsync(
                    clientIp,
                    Request.Headers["User-Agent"].ToString(),
                    "menu",
                    language
                );

                Response.Headers.CacheControl = PublicMenuCacheControl;
                Response.Headers.Vary = "Accept-Language";
                return Ok(menu);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve menu", error = ex.Message });
            }
        }

        /// <summary>
        /// Get public categories only
        /// </summary>
        [HttpGet("public/categories")]
        [AllowAnonymous]
        public async Task<ActionResult> GetPublicCategories([FromQuery] string language = "en")
        {
            try
            {
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var accessCheck = await _menuService.CheckMenuAccessAsync(clientIp, "/api/menu/public/categories");
                if (!accessCheck.IsAllowed)
                    return StatusCode(429, new { message = "Too many requests", resetIn = accessCheck.ResetSeconds });

                var categories = await _menuService.GetPublicCategoriesAsync(language);
                Response.Headers.CacheControl = PublicMenuCacheControl;
                Response.Headers.Vary = "Accept-Language";
                return Ok(categories);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve categories", error = ex.Message });
            }
        }

        /// <summary>
        /// Get public products only
        /// </summary>
        [HttpGet("public/products")]
        [AllowAnonymous]
        public async Task<ActionResult> GetPublicProducts([FromQuery] string language = "en")
        {
            try
            {
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var accessCheck = await _menuService.CheckMenuAccessAsync(clientIp, "/api/menu/public/products");
                if (!accessCheck.IsAllowed)
                    return StatusCode(429, new { message = "Too many requests", resetIn = accessCheck.ResetSeconds });

                var products = await _menuService.GetPublicProductsAsync(language);
                Response.Headers.CacheControl = PublicMenuCacheControl;
                Response.Headers.Vary = "Accept-Language";
                return Ok(products);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve products", error = ex.Message });
            }
        }

        /// <summary>
        /// Get public products by category
        /// </summary>
        [HttpGet("public/categories/{categoryId}/products")]
        [AllowAnonymous]
        public async Task<ActionResult> GetPublicProductsByCategory(Guid categoryId, [FromQuery] string language = "en")
        {
            try
            {
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var accessCheck = await _menuService.CheckMenuAccessAsync(clientIp, "/api/menu/public/products");
                if (!accessCheck.IsAllowed)
                    return StatusCode(429, new { message = "Too many requests", resetIn = accessCheck.ResetSeconds });

                var products = await _menuService.GetPublicProductsByCategoryAsync(categoryId, language);
                Response.Headers.CacheControl = PublicMenuCacheControl;
                Response.Headers.Vary = "Accept-Language";
                return Ok(products);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve products", error = ex.Message });
            }
        }

        /// <summary>
        /// Get tenant branding configuration (logo, colors, etc.)
        /// </summary>
        [HttpGet("public/branding")]
        [AllowAnonymous]
        public async Task<ActionResult<TenantBrandingDto>> GetPublicBranding()
        {
            try
            {
                var branding = await _menuService.GetTenantBrandingAsync();
                return Ok(branding);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve branding", error = ex.Message });
            }
        }

        /// <summary>
        /// Validate menu access (for checking rate limits, access permissions, etc.)
        /// </summary>
        [HttpPost("public/validate-access")]
        [AllowAnonymous]
        public async Task<ActionResult<MenuAccessCheckResponseDto>> ValidateMenuAccess([FromBody] MenuAccessCheckDto dto)
        {
            try
            {
                var result = await _menuService.CheckMenuAccessAsync(dto.ClientIdentifier, dto.Endpoint);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to validate access", error = ex.Message });
            }
        }

        // ─── ADMIN ENDPOINTS (Authenticated + Admin role) ────────

        /// <summary>
        /// Get QR code token for admin use
        /// Requires authentication and Admin role
        /// </summary>
        [HttpPost("qrcode/generate")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> GenerateQrCode([FromBody] string description = null)
        {
            try
            {
                var token = await _menuService.GenerateQrCodeTokenAsync(description);
                return Ok(new { qrToken = token, menuUrl = $"/menu?qr={token}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to generate QR code", error = ex.Message });
            }
        }

        /// <summary>
        /// Get tenant menu settings
        /// Requires authentication and Admin role
        /// </summary>
        [HttpGet("settings")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> GetMenuSettings()
        {
            try
            {
                var settings = await _menuService.GetTenantMenuSettingsAsync();
                return Ok(settings);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve settings", error = ex.Message });
            }
        }

        /// <summary>
        /// Update tenant menu settings
        /// Requires authentication and Admin role
        /// </summary>
        [HttpPut("settings")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> UpdateMenuSettings([FromBody] Models.TenantMenuSettings settings)
        {
            try
            {
                var updated = await _menuService.UpdateTenantMenuSettingsAsync(settings);
                return Ok(updated);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to update settings", error = ex.Message });
            }
        }

        /// <summary>
        /// Check menu access (admin version with detailed info)
        /// Requires authentication
        /// </summary>
        [HttpPost("check-access")]
        [Authorize]
        public async Task<ActionResult<MenuAccessCheckResponseDto>> CheckAccess([FromBody] MenuAccessCheckDto dto)
        {
            try
            {
                var result = await _menuService.CheckMenuAccessAsync(dto.ClientIdentifier, dto.Endpoint);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to check access", error = ex.Message });
            }
        }
    }
}
