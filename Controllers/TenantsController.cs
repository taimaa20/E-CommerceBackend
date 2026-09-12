using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TenantsController : ControllerBase
    {
        private readonly ITenantService _tenantService;
        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenantResolver;

        public TenantsController(ITenantService tenantService, PosDbContext context, ITenantResolver tenantResolver)
        {
            _tenantService = tenantService;
            _context = context;
            _tenantResolver = tenantResolver;
        }

        // Super Admin Only - Create New Tenant (Restaurant)
        [HttpPost]
        [AllowAnonymous] // In production, this should require SuperAdmin role
        public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
        {
            try
            {
                var tenant = await _tenantService.CreateTenantAsync(
                    request.Name,
                    request.Domain,
                    request.OwnerEmail,
                    request.OwnerPassword
                );

                return Ok(new
                {
                    success = true,
                    tenantId = tenant.Id,
                    message = $"Tenant '{tenant.Name}' created successfully",
                    loginUrl = $"https://{tenant.Domain}.yourpos.com"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        [AllowAnonymous] // In production, require SuperAdmin
        public async Task<IActionResult> GetAllTenants()
        {
            var tenants = await _tenantService.GetAllTenantsAsync();
            return Ok(tenants);
        }

        [HttpGet("by-domain/{domain}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByDomain(string domain)
        {
            var tenant = await _tenantService.GetTenantByDomainAsync(domain);
            if (tenant == null) return NotFound("Tenant not found");
            return Ok(tenant);
        }

        // GET api/tenants/branding — public, returns branding for the current tenant
        [HttpGet("branding")]
        [AllowAnonymous]
        public async Task<IActionResult> GetBranding()
        {
            var tenantId = _tenantResolver.GetTenantId();
            var tenant = await _context.Tenants.FindAsync(tenantId);
            if (tenant == null)
                return Ok(new BrandingDto()); // return empty defaults — client uses fallback

            return Ok(new BrandingDto
            {
                LogoUrl = tenant.LogoUrl,
                BusinessName = tenant.BusinessName,
                PrimaryColor = tenant.PrimaryColor,
                PrimaryForeground = tenant.PrimaryForeground,
                AccentColor = tenant.AccentColor,
                UpdatedAt = tenant.CreatedAt.ToString("O") // use createdAt as version seed
            });
        }

        // PUT api/tenants/branding — admin only, saves branding for current tenant
        [HttpPut("branding")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SaveBranding([FromBody] BrandingDto dto)
        {
            var tenantId = _tenantResolver.GetTenantId();

            var tenant = await _context.Tenants.FindAsync(tenantId);
            if (tenant == null)
            {
                // No tenant row yet — create a default one so branding can be saved
                tenant = new Tenant
                {
                    Id = tenantId,
                    Name = dto.BusinessName?.Trim() ?? "My Business",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                };
                _context.Tenants.Add(tenant);
            }

            tenant.LogoUrl = dto.LogoUrl;
            tenant.BusinessName = dto.BusinessName?.Trim();
            tenant.PrimaryColor = dto.PrimaryColor;
            tenant.PrimaryForeground = dto.PrimaryForeground;
            tenant.AccentColor = dto.AccentColor;

            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    public class CreateTenantRequest
    {
        public string Name { get; set; }
        public string Domain { get; set; }
        public string OwnerEmail { get; set; }
        public string OwnerPassword { get; set; }
    }

    public class BrandingDto
    {
        public string? LogoUrl { get; set; }
        public string? BusinessName { get; set; }
        public string? PrimaryColor { get; set; }
        public string? PrimaryForeground { get; set; }
        public string? AccentColor { get; set; }
        public string? UpdatedAt { get; set; }  // ISO string — used as cache version on client
    }
}
