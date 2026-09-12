using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Modules.Dashboard.Services.Export;

namespace RestaurantPos.Api.Services
{
    public interface IRawMaterialConsumptionExportService
    {
        DashboardExportFile BuildExcel(RawMaterialConsumptionReportDto report, IReadOnlyList<RawMaterialConsumptionDetailDto> details, RawMaterialConsumptionQueryDto query, string companyName, string? logoUrl, string generatedBy);
        DashboardExportFile BuildCsv(IReadOnlyList<RawMaterialConsumptionDetailDto> details);
    }
}
