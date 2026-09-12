using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services.Caching
{
    /// <summary>
    /// In-memory cache implementation using IMemoryCache.
    /// Thread-safe, high-performance, suitable for single-instance deployments.
    /// For multi-instance deployments, consider Redis (IDistributedCache).
    /// </summary>
    public class MemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<MemoryCacheService> _logger;
        
        // Track cache keys for pattern-based removal (thread-safe)
        private readonly HashSet<string> _cacheKeys = new();
        private readonly SemaphoreSlim _keysLock = new(1, 1);

        // Default cache configuration
        private static readonly TimeSpan DefaultSlidingExpiration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan DefaultAbsoluteExpiration = TimeSpan.FromHours(1);

        public MemoryCacheService(IMemoryCache memoryCache, ILogger<MemoryCacheService> logger)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<T?> GetOrCreateAsync<T>(
            string key,
            Func<Task<T>> factory,
            TimeSpan? slidingExpiration = null,
            TimeSpan? absoluteExpiration = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            // Try to get from cache first
            if (_memoryCache.TryGetValue(key, out T? cachedValue) && cachedValue != null)
            {
                _logger.LogDebug("Cache HIT: {CacheKey}", key);
                return cachedValue;
            }

            _logger.LogDebug("Cache MISS: {CacheKey}", key);

            // Cache miss - create value using factory
            var value = await factory();

            if (value != null)
            {
                await SetAsync(key, value, slidingExpiration, absoluteExpiration, cancellationToken);
            }

            return value;
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));

            if (_memoryCache.TryGetValue(key, out T? value) && value != null)
            {
                _logger.LogDebug("Cache GET HIT: {CacheKey}", key);
                return Task.FromResult<T?>(value);
            }

            _logger.LogDebug("Cache GET MISS: {CacheKey}", key);
            return Task.FromResult<T?>(null);
        }

        public async Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? slidingExpiration = null,
            TimeSpan? absoluteExpiration = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var cacheOptions = new MemoryCacheEntryOptions
            {
                SlidingExpiration = slidingExpiration ?? DefaultSlidingExpiration,
                AbsoluteExpirationRelativeToNow = absoluteExpiration ?? DefaultAbsoluteExpiration,
                Priority = CacheItemPriority.High // Prevent eviction under memory pressure
            };

            // Register callback to remove from tracking on eviction
            cacheOptions.RegisterPostEvictionCallback(OnCacheEntryEvicted);

            _memoryCache.Set(key, value, cacheOptions);

            // Track key for pattern-based removal
            await _keysLock.WaitAsync(cancellationToken);
            try
            {
                _cacheKeys.Add(key);
            }
            finally
            {
                _keysLock.Release();
            }

            _logger.LogDebug("Cache SET: {CacheKey} (Sliding: {Sliding}, Absolute: {Absolute})", 
                key, 
                slidingExpiration ?? DefaultSlidingExpiration, 
                absoluteExpiration ?? DefaultAbsoluteExpiration);
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            _memoryCache.Remove(key);

            await _keysLock.WaitAsync(cancellationToken);
            try
            {
                _cacheKeys.Remove(key);
            }
            finally
            {
                _keysLock.Release();
            }

            _logger.LogDebug("Cache REMOVE: {CacheKey}", key);
        }

        public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return;

            await _keysLock.WaitAsync(cancellationToken);
            List<string> keysToRemove;
            try
            {
                // Convert pattern to regex-like matching
                var wildcardPattern = pattern.Replace("*", ".*");
                keysToRemove = _cacheKeys
                    .Where(k => System.Text.RegularExpressions.Regex.IsMatch(k, wildcardPattern))
                    .ToList();
            }
            finally
            {
                _keysLock.Release();
            }

            foreach (var key in keysToRemove)
            {
                await RemoveAsync(key, cancellationToken);
            }

            _logger.LogInformation("Cache REMOVE BY PATTERN: {Pattern} ({Count} keys removed)", pattern, keysToRemove.Count);
        }

        public async Task ClearAllAsync(CancellationToken cancellationToken = default)
        {
            await _keysLock.WaitAsync(cancellationToken);
            List<string> allKeys;
            try
            {
                allKeys = _cacheKeys.ToList();
            }
            finally
            {
                _keysLock.Release();
            }

            foreach (var key in allKeys)
            {
                await RemoveAsync(key, cancellationToken);
            }

            _logger.LogWarning("Cache CLEAR ALL: {Count} keys removed", allKeys.Count);
        }

        private void OnCacheEntryEvicted(object key, object value, EvictionReason reason, object state)
        {
            if (key is string keyString)
            {
                _keysLock.Wait();
                try
                {
                    _cacheKeys.Remove(keyString);
                }
                finally
                {
                    _keysLock.Release();
                }

                _logger.LogDebug("Cache EVICTED: {CacheKey} (Reason: {Reason})", keyString, reason);
            }
        }
    }
}
