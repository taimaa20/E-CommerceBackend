using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/public/receipt")]
    [ApiController]
    [EnableRateLimiting("api")]
    public sealed class PublicReceiptController : ControllerBase
    {
        private const string PublicReceiptBaseUrlKey = "PublicReceipt:BaseUrl";
        private const string PublicAppOriginHeader = "X-Public-App-Origin";
        private const string ForwardedHostHeader = "X-Forwarded-Host";
        private const string ForwardedProtoHeader = "X-Forwarded-Proto";

        private readonly IPublicReceiptService _service;
        private readonly ITenantResolver _tenantResolver;
        private readonly IConfiguration _configuration;

        public PublicReceiptController(
            IPublicReceiptService service,
            ITenantResolver tenantResolver,
            IConfiguration configuration)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        [HttpPost("orders/{orderId:guid}/whatsapp-share")]
        [Authorize(Roles = AppRoleGroups.CashierOperators)]
        public async Task<ActionResult<PublicReceiptShareDto>> CreateWhatsAppShare(
            Guid orderId,
            CancellationToken ct)
        {
            var share = await _service.CreateWhatsAppShareAsync(
                orderId,
                _tenantResolver.GetTenantId(),
                IsArabicRequested(Request),
                ResolvePublicBaseUrl(),
                ct);

            return Ok(share);
        }

        [HttpGet("{token}")]
        [AllowAnonymous]
        public async Task<ActionResult<PublicReceiptDto>> GetByToken(string token, CancellationToken ct)
        {
            var receipt = await _service.GetReceiptAsync(token, IsArabicRequested(Request), ct);
            return Ok(receipt);
        }

        private string ResolvePublicBaseUrl()
        {
            var configured = _configuration[PublicReceiptBaseUrlKey];
            if (IsAllowedPublicBaseUrl(configured))
                return configured!.TrimEnd('/');

            var publicAppOrigin = Request.Headers[PublicAppOriginHeader].ToString();
            if (IsAllowedPublicBaseUrl(publicAppOrigin))
                return publicAppOrigin.TrimEnd('/');

            var origin = Request.Headers.Origin.ToString();
            if (IsAllowedPublicBaseUrl(origin))
                return origin.TrimEnd('/');

            var forwardedHost = Request.Headers[ForwardedHostHeader].ToString();
            var forwardedProto = Request.Headers[ForwardedProtoHeader].ToString();
            if (!string.IsNullOrWhiteSpace(forwardedHost) && !string.IsNullOrWhiteSpace(forwardedProto))
            {
                var forwardedBaseUrl = $"{forwardedProto}://{forwardedHost}";
                if (IsAllowedPublicBaseUrl(forwardedBaseUrl))
                    return forwardedBaseUrl.TrimEnd('/');
            }

            var referer = Request.Headers.Referer.ToString();
            if (Uri.TryCreate(referer, UriKind.Absolute, out var refererUri) &&
                IsAllowedPublicBaseUrl($"{refererUri.Scheme}://{refererUri.Authority}"))
            {
                return $"{refererUri.Scheme}://{refererUri.Authority}";
            }

            return $"{Request.Scheme}://{Request.Host}";
        }

        private static bool IsAllowedPublicBaseUrl(string? value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                (uri.IsLoopback || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static bool IsArabicRequested(HttpRequest request)
        {
            var language = request.Headers["Accept-Language"].ToString();
            return language.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        }
    }
}
