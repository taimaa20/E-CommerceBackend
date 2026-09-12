using RestaurantPos.Api.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Settings business logic service with caching support.
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Get system settings (with caching).
        /// </summary>
        Task<SystemSettings> GetSettingsAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update settings and invalidate cache.
        /// </summary>
        Task<SystemSettings> UpdateSettingsAsync(SystemSettings settings, Guid? tenantId = null, CancellationToken cancellationToken = default);
    }
}
