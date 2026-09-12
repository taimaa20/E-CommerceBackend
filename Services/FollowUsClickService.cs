using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services
{
    public class FollowUsClickService : IFollowUsClickService
    {
        private const int PlatformMaxLength = 50;
        private const int UrlMaxLength = 500;
        private const int UserAgentMaxLength = 512;
        private static readonly HashSet<string> AllowedPlatforms = new(StringComparer.OrdinalIgnoreCase)
        {
            "Menu",
            "Instagram",
            "TikTok",
            "Facebook",
            "Website",
            "GoogleMaps",
            "GoogleReviews",
            "WhatsApp",
            "Call",
        };

        private readonly IFollowUsClickRepository _repository;
        private readonly ITenantResolver _tenantResolver;

        public FollowUsClickService(IFollowUsClickRepository repository, ITenantResolver tenantResolver)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
        }

        public async Task TrackClickAsync(FollowUsClickCreateDto dto, string? userAgent, string? referrer, CancellationToken ct)
        {
            var platform = TrimToNull(dto.Platform, PlatformMaxLength);
            var url = TrimToNull(dto.Url, UrlMaxLength);
            if (platform == null || url == null || !AllowedPlatforms.Contains(platform) || !IsSafeDestination(url))
                return;

            var click = new FollowUsClick
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantResolver.GetTenantId(),
                Platform = platform,
                Url = url,
                ClickedAt = DateTime.UtcNow,
                UserAgent = TrimToNull(userAgent, UserAgentMaxLength),
                Referrer = IsSafeWebUrl(referrer) ? TrimToNull(referrer, UrlMaxLength) : null,
            };

            await _repository.AddAsync(click, ct);
        }

        private static bool IsSafeDestination(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                return false;

            return uri.Scheme == Uri.UriSchemeHttp ||
                   uri.Scheme == Uri.UriSchemeHttps ||
                   uri.Scheme.Equals("tel", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSafeWebUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
                return false;

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        private static string? TrimToNull(string? value, int maxLength)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return null;

            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }
    }
}
