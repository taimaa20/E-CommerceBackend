using RestaurantPos.Api.Modules.Dashboard.Security;

namespace RestaurantPos.Api.Options
{
    public sealed class DashboardOptions
    {
        public const string SectionName = "Dashboard";

        public bool EnsureIndexesOnStartup { get; set; }
        public bool ExcludeWasteLogsFromDashboards { get; set; }
        public DashboardScope[] WasteLogExcludedScopes { get; set; } =
        [
            DashboardScope.Operations,
            DashboardScope.Financial
        ];

        public bool ShouldExcludeWasteLogs(DashboardScope scope)
            => ExcludeWasteLogsFromDashboards && WasteLogExcludedScopes?.Contains(scope) == true;
    }
}
