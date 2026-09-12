using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Modules.Marketing.DTOs;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.Marketing.Controllers
{
    [ApiController]
    [Route("api/v1/marketing/settings")]
    [Authorize(Roles = AppRoleGroups.AdminOnly)]
    public sealed class MarketingSettingsController : ControllerBase
    {
        private readonly IMarketingSettingsService _service;
        private readonly IMarketingSeedService _seed;

        public MarketingSettingsController(IMarketingSettingsService service, IMarketingSeedService seed)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _seed = seed ?? throw new ArgumentNullException(nameof(seed));
        }

        [HttpGet]
        public async Task<ActionResult<MarketingSettingsDto>> Get(CancellationToken ct)
            => Ok(await _service.GetAsync(ct));

        [HttpPut]
        public async Task<ActionResult<MarketingSettingsDto>> Update([FromBody] MarketingSettingsUpdateDto dto, CancellationToken ct)
            => Ok(await _service.UpdateAsync(dto, ct));

        /// <summary>Seeds legacy-parity defaults (settings, tiers, base rule). Admin-only; idempotent.</summary>
        [HttpPost("seed-defaults")]
        [Authorize(Roles = AppRoleGroups.AdminStrict)]
        public async Task<ActionResult<MarketingSeedResultDto>> SeedDefaults(CancellationToken ct)
            => Ok(await _seed.SeedLegacyParityAsync(ct));
    }
}
