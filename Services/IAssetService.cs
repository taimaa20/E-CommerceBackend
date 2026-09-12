using Microsoft.AspNetCore.Http;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public interface IAssetService
    {
        Task<PaginatedResponse<AssetListItemDto>> GetPagedAsync(AssetQueryDto query, bool isArabic, AssetActor actor, CancellationToken ct);
        Task<List<AssetListItemDto>> GetExportRowsAsync(AssetQueryDto query, bool isArabic, AssetActor actor, CancellationToken ct);
        string BuildCsv(IEnumerable<AssetListItemDto> rows);
        Task<AssetDashboardDto> GetDashboardAsync(bool isArabic, AssetActor actor, CancellationToken ct);
        Task<AssetDetailsDto> GetDetailsAsync(Guid id, bool isArabic, AssetActor actor, CancellationToken ct);
        Task<AssetDetailsDto> CreateAsync(AssetCreateDto dto, bool isArabic, AssetActor actor, CancellationToken ct);
        Task<AssetDetailsDto> UpdateAsync(Guid id, AssetUpdateDto dto, bool isArabic, AssetActor actor, CancellationToken ct);
        Task SoftDeleteAsync(Guid id, AssetActor actor, CancellationToken ct);
        Task RestoreAsync(Guid id, AssetActor actor, CancellationToken ct);

        Task<List<AssetCategoryDto>> GetCategoriesAsync(bool activeOnly, bool isArabic, CancellationToken ct);
        Task<AssetCategoryDto> CreateCategoryAsync(AssetCategoryCreateDto dto, bool isArabic, AssetActor actor, CancellationToken ct);
        Task<AssetCategoryDto> UpdateCategoryAsync(Guid id, AssetCategoryUpdateDto dto, bool isArabic, AssetActor actor, CancellationToken ct);
        Task DeleteCategoryAsync(Guid id, AssetActor actor, CancellationToken ct);

        Task<AssetAttachmentDto> AddAttachmentAsync(Guid assetId, IFormFile file, string? attachmentType, Guid? maintenanceRecordId, AssetActor actor, CancellationToken ct);
        Task DeleteAttachmentAsync(Guid assetId, Guid attachmentId, AssetActor actor, CancellationToken ct);
        Task<(Stream Stream, string FileName, string MimeType)> OpenAttachmentForDownloadAsync(Guid assetId, Guid attachmentId, AssetActor actor, CancellationToken ct);

        Task<PaginatedResponse<AssetMaintenanceRecordDto>> GetMaintenanceAsync(AssetMaintenanceQueryDto query, bool isArabic, AssetActor actor, CancellationToken ct);
        Task<AssetMaintenanceRecordDto> AddMaintenanceAsync(Guid assetId, AssetMaintenanceCreateDto dto, bool isArabic, AssetActor actor, CancellationToken ct);
        Task<PaginatedResponse<AssetActivityLogDto>> GetLogsAsync(AssetActivityLogQueryDto query, bool isArabic, AssetActor actor, CancellationToken ct);
    }
}
