using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers;

/// <summary>
/// Back-office management of the storefront's promotional banners. The public read lives on
/// <c>GET /api/online-shopping/banners</c> and only ever returns banners that are live.
/// </summary>
[ApiController]
[Route("api/storefront-banners")]
[Authorize(Roles = AppRoleGroups.AdminOnly)]
public sealed class StorefrontBannersController : ControllerBase
{
    private readonly IStorefrontBannerService _service;
    private readonly ITenantResolver _tenantResolver;

    public StorefrontBannersController(
        IStorefrontBannerService service,
        ITenantResolver tenantResolver)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StorefrontBannerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(_tenantResolver.GetTenantId(), ct));

    [HttpPost]
    [ProducesResponseType(typeof(StorefrontBannerDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(StorefrontBannerUpsertDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.CreateAsync(_tenantResolver.GetTenantId(), dto, ct));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(StorefrontBannerDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, StorefrontBannerUpsertDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpdateAsync(_tenantResolver.GetTenantId(), id, dto, ct));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(_tenantResolver.GetTenantId(), id, ct);
        return Ok(new { message = "Banner removed." });
    }
}
