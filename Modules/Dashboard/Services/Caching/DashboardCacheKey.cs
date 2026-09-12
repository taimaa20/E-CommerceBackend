using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RestaurantPos.Api.Modules.Dashboard.Filters;
using RestaurantPos.Api.Modules.Dashboard.Security;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Caching
{
    /// <summary>
    /// Deterministic cache-key builder for dashboard metrics.
    /// Key shape: <c>dash:{tenant}:{scope}:{metric}:{filterHash}</c>.
    /// The filter hash is a short SHA-1 prefix of the canonical JSON form of
    /// the filter, so two requests with the same effective filter (regardless
    /// of property ordering) hit the same cache entry.
    /// </summary>
    public static class DashboardCacheKey
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public static string Build(
            Guid tenantId,
            DashboardScope scope,
            string metric,
            DashboardFilterDto filter,
            bool excludeWasteLogs)
        {
            var canon = JsonSerializer.Serialize(new
            {
                Filter = filter,
                ExcludeWasteLogsFromDashboards = excludeWasteLogs
            }, JsonOpts);
            using var sha = SHA1.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canon));
            var hex = BitConverter.ToString(hash, 0, 8).Replace("-", "").ToLowerInvariant();
            return $"dash:{tenantId:N}:{scope}:{metric}:{hex}";
        }

        public static string TenantPrefix(Guid tenantId) => $"dash:{tenantId:N}:*";
        public static string ScopePrefix(Guid tenantId, DashboardScope scope) => $"dash:{tenantId:N}:{scope}:*";
        public static string MetricPrefix(Guid tenantId, DashboardScope scope, string metric)
            => $"dash:{tenantId:N}:{scope}:{metric}:*";
    }
}
