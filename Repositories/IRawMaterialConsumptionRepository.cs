using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Repositories
{
    public interface IRawMaterialConsumptionRepository
    {
        Task<RawMaterialConsumptionReportDto?> GetAsync(
            RawMaterialConsumptionQueryDto query,
            CancellationToken ct);
        Task<IReadOnlyList<RawMaterialConsumptionDetailDto>> GetAllDetailsAsync(
            RawMaterialConsumptionQueryDto query,
            CancellationToken ct);
        Task<(string CompanyName, string? LogoUrl)> GetBrandingAsync(Guid tenantId, CancellationToken ct);
    }
}
