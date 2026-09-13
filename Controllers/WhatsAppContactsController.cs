using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers;

/// <summary>
/// Back-office management of WhatsApp destinations. The public storefront reads them through
/// the online-shopping route it already calls — there is no public route here.
/// </summary>
[ApiController]
[Route("api/whatsapp-contacts")]
[Authorize(Roles = AppRoleGroups.AdminOnly)]
public sealed class WhatsAppContactsController : ControllerBase
{
    private readonly IWhatsAppContactService _service;
    private readonly ITenantResolver _tenantResolver;

    public WhatsAppContactsController(IWhatsAppContactService service, ITenantResolver tenantResolver)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WhatsAppContactDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _service.ListAsync(_tenantResolver.GetTenantId(), ct));

    [HttpPost]
    [ProducesResponseType(typeof(WhatsAppContactDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] WhatsAppContactUpsertDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.CreateAsync(_tenantResolver.GetTenantId(), dto, ct));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(WhatsAppContactDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] WhatsAppContactUpsertDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpdateAsync(_tenantResolver.GetTenantId(), id, dto, ct));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(_tenantResolver.GetTenantId(), id, ct);
        return Ok(new { message = "WhatsApp destination removed." });
    }
}
