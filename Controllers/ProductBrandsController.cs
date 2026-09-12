using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers;

/// <summary>
/// Back-office management of brand identity (name, Arabic name, logo). The storefront reads
/// brands through the catalogue endpoints it already calls — there is no public brand route.
/// </summary>
[ApiController]
[Route("api/product-brands")]
[Authorize(Roles = AppRoleGroups.AdminOnly)]
public sealed class ProductBrandsController : ControllerBase
{
    private readonly IProductBrandService _service;
    private readonly ITenantResolver _tenantResolver;

    public ProductBrandsController(
        IProductBrandService service,
        ITenantResolver tenantResolver)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductBrandDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(_tenantResolver.GetTenantId(), ct));

    [HttpGet("unregistered")]
    [ProducesResponseType(typeof(IReadOnlyList<UnregisteredProductBrandDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnregistered(CancellationToken ct)
        => Ok(await _service.GetUnregisteredAsync(_tenantResolver.GetTenantId(), ct));

    [HttpPost]
    [ProducesResponseType(typeof(ProductBrandDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(ProductBrandUpsertDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.CreateAsync(_tenantResolver.GetTenantId(), dto, ct));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProductBrandDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, ProductBrandUpsertDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpdateAsync(_tenantResolver.GetTenantId(), id, dto, ct));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(_tenantResolver.GetTenantId(), id, ct);
        return Ok(new { message = "Brand removed." });
    }
}
