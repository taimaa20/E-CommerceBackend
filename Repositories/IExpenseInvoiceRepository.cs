using RestaurantPos.Api.DTOs.ExpenseInvoice;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public interface IExpenseInvoiceRepository
    {
        Task<bool> PotentialDuplicateExistsAsync(
            Guid branchId,
            Guid? supplierId,
            string? manualSupplierName,
            string invoiceNumber,
            DateTime invoiceDate,
            Guid? excludeId,
            CancellationToken ct = default);

        Task<DuplicateInvoiceBehavior> GetDuplicateInvoiceBehaviorAsync(
            Guid tenantId,
            CancellationToken ct = default);

        Task AddAsync(ExpenseInvoice invoice, CancellationToken ct = default);

        Task AddAuditLogAsync(ExpenseInvoiceAuditLog auditLog, CancellationToken ct = default);

        Task<ExpenseInvoice?> GetTrackedByIdAsync(Guid id, CancellationToken ct = default);

        Task<ExpenseInvoiceDetailsDto?> GetDetailsAsync(Guid id, bool isArabic, Func<string, string?> publicUrlBuilder, string downloadRouteBase, CancellationToken ct = default);

        Task<PaginatedResponse<ExpenseInvoiceListItemDto>> GetPagedAsync(
            ExpenseInvoiceQueryDto query,
            bool isArabic,
            Func<string, string?> publicUrlBuilder,
            CancellationToken ct = default);

        Task<ExpenseInvoiceStatisticsDto> GetStatisticsAsync(string defaultCurrency, Guid? branchId, CancellationToken ct = default);

        Task<ExpenseInvoiceAttachment?> GetAttachmentAsync(Guid invoiceId, Guid attachmentId, CancellationToken ct = default);

        Task AddAttachmentAsync(ExpenseInvoiceAttachment attachment, CancellationToken ct = default);

        Task RemoveAttachmentAsync(ExpenseInvoiceAttachment attachment, CancellationToken ct = default);

        Task<int> SaveChangesAsync(CancellationToken ct = default);
    }
}
