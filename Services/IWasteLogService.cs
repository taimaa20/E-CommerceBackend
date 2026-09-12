using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public interface IWasteLogService
    {
        Task<WasteLogPageDto> GetPagedAsync(WasteLogQueryDto query, CancellationToken ct = default);
        Task<WasteLogDto> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<WasteLogSummaryDto> GetSummaryAsync(WasteLogQueryDto query, CancellationToken ct = default);
        Task<WasteLogAnalyticsDto> GetAnalyticsAsync(WasteLogQueryDto query, CancellationToken ct = default);
        Task<byte[]> ExportCsvAsync(WasteLogQueryDto query, CancellationToken ct = default);
        Task<IReadOnlyList<WasteEmployeeOptionDto>> SearchEmployeesAsync(string? search, int limit, CancellationToken ct = default);
        Task<WasteLogDto> CreateAsync(WasteLogCreateDto request, Guid? userId, string? userName, CancellationToken ct = default);
        Task<WasteLogDto> UpdateAsync(Guid id, WasteLogUpdateDto request, Guid? userId, string? userName, CancellationToken ct = default);
        Task<WasteLogDto> ApproveAsync(Guid id, WasteLogDecisionDto request, Guid? userId, string? userName, CancellationToken ct = default);
        Task<WasteLogDto> RejectAsync(Guid id, WasteLogDecisionDto request, Guid? userId, string? userName, CancellationToken ct = default);
    }
}
