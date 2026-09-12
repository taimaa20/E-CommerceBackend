using RestaurantPos.Api.Modules.Dashboard.DTOs.Financial;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Financial
{
    public interface IFinancialDashboardExportService
    {
        FinancialDashboardExportFile BuildWorkbook(
            FinancialSnapshotDto snapshot,
            string? currency,
            CommissionAnalysisDto? commission = null);
    }

    public sealed record FinancialDashboardExportFile(
        byte[] Content,
        string FileName,
        string ContentType);
}
