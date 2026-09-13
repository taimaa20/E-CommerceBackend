using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers;

/// <summary>
/// Currency lookup management.
///
/// There is no DELETE: a currency is withdrawn by deactivating it, which keeps every record
/// that already names the code readable. Nor is there a way to edit a code — see
/// <see cref="CurrencyUpdateDto"/>.
/// </summary>
[ApiController]
[Route("api/currencies")]
[Authorize(Roles = AppRoleGroups.AdminOnly)]
public sealed class CurrenciesController : ControllerBase
{
    private readonly ICurrencyService _service;
    private readonly ITenantResolver _tenantResolver;

    public CurrenciesController(ICurrencyService service, ITenantResolver tenantResolver)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
    }

    /// GET: api/currencies?activeOnly=true — selectors ask for the active list so no consumer
    /// has to filter deactivated rows itself. Management reads the full list.
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CurrencyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] bool activeOnly = false, CancellationToken ct = default)
        => Ok(await _service.ListAsync(
            _tenantResolver.GetTenantId(),
            activeOnly,
            GeneralHelper.IsArabicRequested(Request),
            ct));

    [HttpPost]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CurrencyCreateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.CreateAsync(
            _tenantResolver.GetTenantId(),
            dto,
            GeneralHelper.IsArabicRequested(Request),
            ct));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CurrencyUpdateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpdateAsync(
            _tenantResolver.GetTenantId(),
            id,
            dto,
            GeneralHelper.IsArabicRequested(Request),
            ct));
    }
}
