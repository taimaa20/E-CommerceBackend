using System;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services.Caching
{
    /// <summary>
    /// Abstraction for caching operations. Supports multiple cache providers (Memory, Redis, etc.)
    /// Thread-safe and async-first design for high-performance scenarios.
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Get value from cache or create it if not found (atomic operation).
        /// </summary>
        Task<T?> GetOrCreateAsync<T>(
            string key,
            Func<Task<T>> factory,
            TimeSpan? slidingExpiration = null,
            TimeSpan? absoluteExpiration = null,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Get value from cache by key.
        /// </summary>
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Set value in cache with optional expiration.
        /// </summary>
        Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? slidingExpiration = null,
            TimeSpan? absoluteExpiration = null,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Remove specific cache entry.
        /// </summary>
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Remove all cache entries matching a pattern (e.g., "products:*").
        /// WARNING: Pattern matching is expensive - use sparingly.
        /// </summary>
        Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clear all cache entries. Use only in testing or critical scenarios.
        /// </summary>
        Task ClearAllAsync(CancellationToken cancellationToken = default);
    }
}
