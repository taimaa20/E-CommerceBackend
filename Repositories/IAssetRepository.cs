using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public interface IAssetRepository
    {
        Task<int> CountCreatedOnDateAsync(DateTime dayUtc, CancellationToken ct);
        Task<bool> AssetCodeExistsAsync(string assetCode, Guid? excludeId, CancellationToken ct);
        Task<bool> CategoryExistsAsync(Guid categoryId, CancellationToken ct);
        Task<bool> StaffProfileExistsAsync(Guid staffProfileId, CancellationToken ct);
        Task<bool> CanUserAccessAssetAsync(Guid assetId, Guid userId, CancellationToken ct);

        Task AddAssetAsync(Asset asset, CancellationToken ct);
        Task<Asset?> GetTrackedAssetAsync(Guid id, Guid branchId, CancellationToken ct);
        Task<Asset?> GetTrackedAssetIgnoringFiltersAsync(Guid id, Guid branchId, CancellationToken ct);
        Task<AssetDetailsDto?> GetDetailsAsync(Guid id, Guid branchId, bool isArabic, string downloadRouteBase, Func<string, string?> publicUrlBuilder, CancellationToken ct);
        Task<PaginatedResponse<AssetListItemDto>> GetPagedAsync(AssetQueryDto query, Guid branchId, bool isArabic, Guid? assignedUserId, CancellationToken ct);
        Task<List<AssetListItemDto>> GetExportRowsAsync(AssetQueryDto query, Guid branchId, bool isArabic, Guid? assignedUserId, int maxRows, CancellationToken ct);
        Task<AssetDashboardDto> GetDashboardAsync(Guid branchId, bool isArabic, Guid? assignedUserId, CancellationToken ct);

        Task<List<AssetCategoryDto>> GetCategoriesAsync(bool activeOnly, bool isArabic, CancellationToken ct);
        Task<AssetCategory?> GetTrackedCategoryAsync(Guid id, CancellationToken ct);
        Task AddCategoryAsync(AssetCategory category, CancellationToken ct);
        Task<bool> CategoryNameExistsAsync(string nameEn, string nameAr, Guid? excludeId, CancellationToken ct);
        Task<bool> CategoryInUseAsync(Guid id, CancellationToken ct);
        Task<bool> HasAnyCategoryAsync(CancellationToken ct);

        Task AddAttachmentAsync(AssetAttachment attachment, CancellationToken ct);
        Task<AssetAttachment?> GetAttachmentAsync(Guid assetId, Guid attachmentId, CancellationToken ct);
        Task RemoveAttachmentAsync(AssetAttachment attachment, CancellationToken ct);

        Task AddMaintenanceAsync(AssetMaintenanceRecord maintenance, CancellationToken ct);
        Task<PaginatedResponse<AssetMaintenanceRecordDto>> GetMaintenancePagedAsync(AssetMaintenanceQueryDto query, Guid branchId, bool isArabic, Guid? assignedUserId, CancellationToken ct);
        Task<List<AssetMaintenanceRecordDto>> GetMaintenanceForAssetAsync(Guid assetId, bool isArabic, CancellationToken ct);

        Task AddLogAsync(AssetActivityLog log, CancellationToken ct);
        Task<PaginatedResponse<AssetActivityLogDto>> GetLogsPagedAsync(AssetActivityLogQueryDto query, Guid branchId, bool isArabic, Guid? assignedUserId, CancellationToken ct);

        Task<int> SaveChangesAsync(CancellationToken ct);
    }
}
