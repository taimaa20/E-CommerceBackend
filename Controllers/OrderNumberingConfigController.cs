using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;
using RestaurantPos.Api.Services.Time;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/order-numbering-config")]
    [ApiController]
    [Authorize(Roles = AppRoleGroups.OrderNumberConfigManagers)]
    public class OrderNumberingConfigController : ControllerBase
    {
        private readonly IOrderNumberingConfigService _service;
        private readonly ISettingsService _settingsService;
        private readonly ITenantResolver _tenantResolver;
        private readonly ILogger<OrderNumberingConfigController> _logger;

        public OrderNumberingConfigController(
            IOrderNumberingConfigService service,
            ISettingsService settingsService,
            ITenantResolver tenantResolver,
            ILogger<OrderNumberingConfigController> logger)
        {
            _service        = service        ?? throw new ArgumentNullException(nameof(service));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _logger         = logger         ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<ActionResult<OrderNumberingConfigDto>> Get(CancellationToken ct)
        {
            var tenantId = _tenantResolver.GetTenantId();
            var config = await _service.GetForTenantAsync(tenantId, ct);
            var settings = await _settingsService.GetSettingsAsync(tenantId, ct);
            var dto = ToDto(config);
            dto.PreviewExample = BuildPreview(config, ResolveLocalNow(settings.TimeZoneId));
            return Ok(dto);
        }

        [HttpPut]
        public async Task<ActionResult<OrderNumberingConfigDto>> Update(
            [FromBody] OrderNumberingConfigDto dto,
            CancellationToken ct)
        {
            if (dto is null) return BadRequest("Body is required.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var tenantId = _tenantResolver.GetTenantId();

            var saved = await _service.UpsertAsync(tenantId, new OrderNumberingConfig
            {
                ResetStrategy      = dto.ResetStrategy,
                Scope              = dto.Scope,
                Prefix             = dto.Prefix,
                IncludeDate        = dto.IncludeDate,
                IncludeMonth       = dto.IncludeMonth,
                IncludeYear        = dto.IncludeYear,
                IncludeShiftNumber = dto.IncludeShiftNumber,
                IncludeBranchCode  = dto.IncludeBranchCode,
                BranchCode         = dto.BranchCode,
            }, ct);

            var settings = await _settingsService.GetSettingsAsync(tenantId, ct);
            var result = ToDto(saved);
            result.PreviewExample = BuildPreview(saved, ResolveLocalNow(settings.TimeZoneId));
            return Ok(result);
        }

        private static OrderNumberingConfigDto ToDto(OrderNumberingConfig? c) => new()
        {
            ResetStrategy      = c?.ResetStrategy      ?? OrderNumberResetStrategy.Monthly,
            Scope              = c?.Scope              ?? OrderNumberSequenceScope.PerOrderType,
            Prefix             = c?.Prefix,
            IncludeDate        = c?.IncludeDate        ?? false,
            IncludeMonth       = c?.IncludeMonth       ?? true,
            IncludeYear        = c?.IncludeYear        ?? true,
            IncludeShiftNumber = c?.IncludeShiftNumber ?? false,
            IncludeBranchCode  = c?.IncludeBranchCode  ?? false,
            BranchCode         = c?.BranchCode,
        };

        // Render a sample for the UI without touching the counter table.
        // Mirrors OrderDisplayNumberService.Format with assigned=1 and shift=1.
        private static string BuildPreview(OrderNumberingConfig? c, DateTime localNow)
        {
            var channelCode = OrderDisplayNumberService.CodeTakeaway;

            if (c is null)
                return $"{channelCode}-{localNow:yyyyMM}-1";

            var parts = new List<string>(6);
            if (c.IncludeBranchCode && !string.IsNullOrWhiteSpace(c.BranchCode))
                parts.Add(c.BranchCode!.Trim());

            parts.Add(!string.IsNullOrWhiteSpace(c.Prefix)
                ? c.Prefix!.Trim()
                : c.Scope == OrderNumberSequenceScope.Global
                    ? OrderDisplayNumberService.CodeGlobalDisplay
                    : channelCode);

            var datePart = ResolveDatePart(c, localNow);
            if (datePart != null) parts.Add(datePart);

            if (c.IncludeShiftNumber) parts.Add("S001");

            parts.Add("1");
            return string.Join("-", parts);
        }

        private static DateTime ResolveLocalNow(string? timeZoneId)
            => TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                RestaurantTimeZone.Resolve(timeZoneId));

        private static string? ResolveDatePart(OrderNumberingConfig config, DateTime utcNow)
        {
            if (config.IncludeDate)
                return utcNow.ToString("yyyyMMdd");
            if (config.IncludeYear && config.IncludeMonth)
                return utcNow.ToString("yyyyMM");
            if (config.IncludeYear)
                return utcNow.ToString("yyyy");
            return config.IncludeMonth ? utcNow.ToString("MM") : null;
        }
    }
}
