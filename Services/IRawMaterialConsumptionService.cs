using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public interface IRawMaterialConsumptionService
    {
        Task<RawMaterialConsumptionReportDto> GetAsync(RawMaterialConsumptionQueryDto query, CancellationToken ct);
        Task<(RawMaterialConsumptionReportDto Report, IReadOnlyList<RawMaterialConsumptionDetailDto> Details, string CompanyName, string? LogoUrl)> GetExportAsync(
            RawMaterialConsumptionQueryDto query, CancellationToken ct);
    }
}
