using Microsoft.Extensions.Logging;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Services.Caching;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Settings service with in-memory caching.
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private readonly ISettingsRepository _repository;
        private readonly ICacheService _cache;
        private readonly ILogger<SettingsService> _logger;

        public SettingsService(
            ISettingsRepository repository,
            ICacheService cache,
            ILogger<SettingsService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SystemSettings> GetSettingsAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
        {
            var cacheKey = CacheKeys.SettingsAll(tenantId);

            return await _cache.GetOrCreateAsync(
                cacheKey,
                async () =>
                {
                    var settings = await _repository.GetSettingsAsync(tenantId, cancellationToken);

                    // Auto-create defaults if not found
                    if (settings == null)
                    {
                        settings = new SystemSettings
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId
                        };
                        settings = await _repository.SaveSettingsAsync(
                            settings,
                            tenantId,
                            cancellationToken);
                    }

                    return settings;
                },
                slidingExpiration: TimeSpan.FromMinutes(30),
                absoluteExpiration: TimeSpan.FromHours(2),
                cancellationToken)
                ?? throw new InvalidOperationException("Settings cache returned no value.");
        }

        public async Task<SystemSettings> UpdateSettingsAsync(SystemSettings settings, Guid? tenantId = null, CancellationToken cancellationToken = default)
        {
            settings.UpdatedAt = DateTime.UtcNow;

            var updated = await _repository.SaveSettingsAsync(
                settings,
                tenantId,
                cancellationToken);

            // Invalidate cache
            var cacheKey = CacheKeys.SettingsAll(tenantId);
            await _cache.RemoveAsync(cacheKey, cancellationToken);

            _logger.LogInformation("Settings cache invalidated for tenant {TenantId}", tenantId);

            return updated;
        }
    }
}
