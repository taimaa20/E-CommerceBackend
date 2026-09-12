namespace RestaurantPos.Api.Modules.Dashboard.Services.Caching
{
    /// <summary>
    /// Tiered TTLs as defined in the dashboard architecture spec.
    /// Centralising them prevents per-calculator drift.
    /// </summary>
    public static class DashboardCacheTtl
    {
        /// <summary>5s — live operations widgets (active orders, kitchen queue).</summary>
        public static readonly TimeSpan Live       = TimeSpan.FromSeconds(5);
        /// <summary>30s — hot widgets that change minutely but tolerate a small lag.</summary>
        public static readonly TimeSpan Short      = TimeSpan.FromSeconds(30);
        /// <summary>5min — financial / inventory snapshots.</summary>
        public static readonly TimeSpan Medium     = TimeSpan.FromMinutes(5);
        /// <summary>1h — audit (not real-time by design).</summary>
        public static readonly TimeSpan Long       = TimeSpan.FromHours(1);
        /// <summary>24h — closed-day data that cannot change.</summary>
        public static readonly TimeSpan Historical = TimeSpan.FromHours(24);
    }
}
