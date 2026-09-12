using RestaurantPos.Api.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Repositories
{
    /// <summary>
    /// Repository abstraction for Settings data access.
    /// </summary>
    public interface ISettingsRepository
    {
        /// <summary>
        /// Get system settings (first record or by tenant).
        /// </summary>
        Task<SystemSettings?> GetSettingsAsync(
            Guid? tenantId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Create or update settings.
        /// </summary>
        Task<SystemSettings> SaveSettingsAsync(
            SystemSettings settings,
            Guid? tenantId,
            CancellationToken cancellationToken = default);
    }
}
