using Microsoft.AspNetCore.Http;
using RestaurantPos.Api.DTOs.ExpenseInvoice;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public interface IExpenseInvoiceService
    {
        Task<ExpenseInvoiceDetailsDto> CreateAsync(
            CreateExpenseInvoiceDto dto,
            Guid? userId,
            string? userName,
            bool isArabic,
            CancellationToken ct);

        Task<ExpenseInvoiceDetailsDto> UpdateAsync(
            Guid id,
            UpdateExpenseInvoiceDto dto,
            Guid? userId,
            string? userName,
            bool isArabic,
            CancellationToken ct);
        Task SoftDeleteAsync(Guid id, CancellationToken ct);

        Task<ExpenseInvoiceDetailsDto> UpdatePaymentAsync(
            Guid id,
            UpdateExpenseInvoicePaymentDto dto,
            bool isArabic,
            CancellationToken ct);

        Task<ExpenseInvoiceDetailsDto> GetDetailsAsync(Guid id, bool isArabic, CancellationToken ct);
        Task<PaginatedResponse<ExpenseInvoiceListItemDto>> GetPagedAsync(ExpenseInvoiceQueryDto query, bool isArabic, CancellationToken ct);
        Task<ExpenseInvoiceStatisticsDto> GetStatisticsAsync(Guid? branchId, CancellationToken ct);

        Task<ExpenseInvoiceAttachmentDto> AddAttachmentAsync(Guid invoiceId, IFormFile file, Guid? userId, CancellationToken ct);
        Task DeleteAttachmentAsync(Guid invoiceId, Guid attachmentId, CancellationToken ct);

        Task<(Stream Stream, string FileName, string MimeType)> OpenAttachmentForDownloadAsync(
            Guid invoiceId, Guid attachmentId, CancellationToken ct);
    }
}
