using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs.ExpenseInvoice;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    /// <summary>
    /// Data access for ExpenseInvoice + ExpenseInvoiceAttachment.
    ///
    /// Conventions:
    ///   • Read paths use AsNoTracking() + projection — never full entity load.
    ///   • Write paths return the tracked entity for the service to mutate.
    ///   • All queries inherit the global tenant + soft-delete filter from
    ///     PosDbContext — no manual TenantId predicate needed.
    /// </summary>
    public sealed class ExpenseInvoiceRepository : IExpenseInvoiceRepository
    {
        private readonly PosDbContext _context;

        public ExpenseInvoiceRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task<bool> PotentialDuplicateExistsAsync(
            Guid branchId,
            Guid? supplierId,
            string? manualSupplierName,
            string invoiceNumber,
            DateTime invoiceDate,
            Guid? excludeId,
            CancellationToken ct = default)
        {
            var dayStart = invoiceDate.Date;
            var dayEnd = dayStart.AddDays(1);
            var rows = _context.ExpenseInvoices
                .AsNoTracking()
                .Where(i => i.InvoiceNumber == invoiceNumber
                         && i.BranchId == branchId
                         && i.InvoiceDate >= dayStart
                         && i.InvoiceDate < dayEnd
                         && (excludeId == null || i.Id != excludeId.Value));

            if (supplierId.HasValue)
                return rows.AnyAsync(i => i.SupplierId == supplierId.Value, ct);

            var supplierName = manualSupplierName?.Trim();
            return string.IsNullOrEmpty(supplierName)
                ? Task.FromResult(false)
                : rows.AnyAsync(i => i.SupplierId == null && i.SupplierName == supplierName, ct);
        }

        public async Task<DuplicateInvoiceBehavior> GetDuplicateInvoiceBehaviorAsync(
            Guid tenantId,
            CancellationToken ct = default)
        {
            var settings = _context.SystemSettings.AsNoTracking();
            var tenantBehavior = await settings
                .Where(s => s.TenantId == tenantId)
                .Select(s => (DuplicateInvoiceBehavior?)s.DuplicateInvoiceBehavior)
                .FirstOrDefaultAsync(ct);

            if (tenantBehavior.HasValue)
                return tenantBehavior.Value;

            return await settings
                .Where(s => s.TenantId == null)
                .Select(s => (DuplicateInvoiceBehavior?)s.DuplicateInvoiceBehavior)
                .FirstOrDefaultAsync(ct)
                ?? DuplicateInvoiceBehavior.WarningOnly;
        }

        public Task AddAsync(ExpenseInvoice invoice, CancellationToken ct = default)
            => _context.ExpenseInvoices.AddAsync(invoice, ct).AsTask();

        public Task AddAuditLogAsync(ExpenseInvoiceAuditLog auditLog, CancellationToken ct = default)
            => _context.ExpenseInvoiceAuditLogs.AddAsync(auditLog, ct).AsTask();

        public Task<ExpenseInvoice?> GetTrackedByIdAsync(Guid id, CancellationToken ct = default)
            => _context.ExpenseInvoices
                .Include(i => i.Attachments)
                .FirstOrDefaultAsync(i => i.Id == id, ct);

        public async Task<ExpenseInvoiceDetailsDto?> GetDetailsAsync(
            Guid id,
            bool isArabic,
            Func<string, string?> publicUrlBuilder,
            string downloadRouteBase,
            CancellationToken ct = default)
        {
            // Single projection — no entity materialisation, no N+1 on attachments
            // because the inner Select is translated as a sub-query.
            //
            // Locale resolution: live FK → NameAr when isArabic and present, else
            // live Name. Falls back to the stored English snapshot for legacy /
            // manual rows that have no FK.
            var dto = await _context.ExpenseInvoices
                .AsNoTracking()
                .Where(i => i.Id == id)
                .Select(i => new ExpenseInvoiceDetailsDto
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    SupplierName = i.Supplier != null
                        ? (isArabic && i.Supplier.NameAr != null && i.Supplier.NameAr != string.Empty
                            ? i.Supplier.NameAr
                            : i.Supplier.Name)
                        : i.SupplierName,
                    SupplierPhone = i.SupplierPhone,
                    SupplierTaxNumber = i.SupplierTaxNumber,
                    SupplierId = i.SupplierId,
                    ExpenseCategoryId = i.ExpenseCategoryId,
                    CategoryLabel = i.ExpenseCategory != null
                        ? (isArabic && i.ExpenseCategory.NameAr != null && i.ExpenseCategory.NameAr != string.Empty
                            ? i.ExpenseCategory.NameAr
                            : i.ExpenseCategory.Name)
                        : i.CategoryLabel,
                    InvoiceDate = i.InvoiceDate,
                    DueDate = i.DueDate,
                    Subtotal = i.Subtotal,
                    TaxAmount = i.TaxAmount,
                    DiscountAmount = i.DiscountAmount,
                    TotalAmount = i.TotalAmount,
                    PaidAmount = i.PaidAmount,
                    Balance = i.TotalAmount - i.PaidAmount,
                    PurchaseOrderId = i.PurchaseOrderId,
                    PurchaseOrderNumber = i.PurchaseOrder != null
                        ? i.PurchaseOrder.OrderNumber
                        : null,
                    CurrencyCode = i.CurrencyCode,
                    PaymentMethod = i.PaymentMethod.ToString(),
                    PaymentMethodDetail = i.PaymentMethodDetail,
                    Notes = i.Notes,
                    Status = i.Status.ToString(),
                    BranchId = i.BranchId,
                    CreatedById = i.CreatedById,
                    CreatedByName = i.CreatedByUser != null ? i.CreatedByUser.Username : null,
                    CreatedAt = i.CreatedAt,
                    UpdatedAt = i.UpdatedAt,
                    Attachments = i.Attachments
                        .OrderByDescending(a => a.IsPrimary)
                        .ThenBy(a => a.UploadedAt)
                        .Select(a => new ExpenseInvoiceAttachmentDto
                        {
                            Id = a.Id,
                            ExpenseInvoiceId = a.ExpenseInvoiceId,
                            OriginalFileName = a.OriginalFileName,
                            FileExtension = a.FileExtension,
                            FileSize = a.FileSize,
                            MimeType = a.MimeType,
                            UploadedAt = a.UploadedAt,
                            UploadedByName = a.UploadedByUser != null ? a.UploadedByUser.Username : null,
                            IsPrimary = a.IsPrimary,
                            // RelativePath flows into helpers below; the actual
                            // URL is composed in the controller via publicUrlBuilder.
                            Url = a.RelativePath,
                            DownloadUrl = a.Id.ToString()
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync(ct);

            if (dto is null) return null;

            // Rewrite the placeholder fields into real URLs. Doing this outside
            // the projection keeps the SQL clean and lets us swap providers
            // without touching the query shape.
            foreach (var att in dto.Attachments)
            {
                var relativePath = att.Url;
                att.Url = publicUrlBuilder(relativePath) ?? string.Empty;
                att.DownloadUrl = $"{downloadRouteBase}/{dto.Id}/attachments/{att.DownloadUrl}/download";
            }

            return dto;
        }

        public async Task<PaginatedResponse<ExpenseInvoiceListItemDto>> GetPagedAsync(
            ExpenseInvoiceQueryDto query,
            bool isArabic,
            Func<string, string?> publicUrlBuilder,
            CancellationToken ct = default)
        {
            var page = Math.Max(query.Page, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, 200);

            var rows = _context.ExpenseInvoices.AsNoTracking();
            rows = ApplyFilters(rows, query);

            var total = await rows.CountAsync(ct);

            var items = await rows
                .OrderByDescending(i => i.InvoiceDate)
                .ThenByDescending(i => i.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new ExpenseInvoiceListItemDto
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    // Locale-aware live names (NameAr when isArabic + present,
                    // else Name). Falls back to stored snapshot for legacy / manual rows.
                    SupplierName = i.Supplier != null
                        ? (isArabic && i.Supplier.NameAr != null && i.Supplier.NameAr != string.Empty
                            ? i.Supplier.NameAr
                            : i.Supplier.Name)
                        : i.SupplierName,
                    SupplierId = i.SupplierId,
                    CategoryLabel = i.ExpenseCategory != null
                        ? (isArabic && i.ExpenseCategory.NameAr != null && i.ExpenseCategory.NameAr != string.Empty
                            ? i.ExpenseCategory.NameAr
                            : i.ExpenseCategory.Name)
                        : i.CategoryLabel,
                    ExpenseCategoryId = i.ExpenseCategoryId,
                    InvoiceDate = i.InvoiceDate,
                    DueDate = i.DueDate,
                    TotalAmount = i.TotalAmount,
                    PaidAmount = i.PaidAmount,
                    Balance = i.TotalAmount - i.PaidAmount,
                    CurrencyCode = i.CurrencyCode,
                    Status = i.Status.ToString(),
                    PaymentMethod = i.PaymentMethod.ToString(),
                    AttachmentsCount = i.Attachments.Count,
                    PrimaryAttachmentUrl = i.Attachments
                        .Where(a => a.IsPrimary)
                        .Select(a => a.RelativePath)
                        .FirstOrDefault()
                        ?? i.Attachments
                            .OrderBy(a => a.UploadedAt)
                            .Select(a => a.RelativePath)
                            .FirstOrDefault(),
                    CreatedByName = i.CreatedByUser != null ? i.CreatedByUser.Username : null,
                    CreatedAt = i.CreatedAt,
                    PurchaseOrderId = i.PurchaseOrderId,
                    PurchaseOrderNumber = i.PurchaseOrder != null
                        ? i.PurchaseOrder.OrderNumber
                        : null
                })
                .ToListAsync(ct);

            // Convert RelativePath -> absolute URL for the thumb. Cheap O(n)
            // post-processing; keeps the query translatable to pure SQL.
            foreach (var row in items)
            {
                if (!string.IsNullOrEmpty(row.PrimaryAttachmentUrl))
                    row.PrimaryAttachmentUrl = publicUrlBuilder(row.PrimaryAttachmentUrl);
            }

            return new PaginatedResponse<ExpenseInvoiceListItemDto>
            {
                Items = items,
                TotalCount = total,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        public async Task<ExpenseInvoiceStatisticsDto> GetStatisticsAsync(string defaultCurrency, Guid? branchId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var today = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);

            var rows = _context.ExpenseInvoices.AsNoTracking();
            if (branchId.HasValue)
                rows = rows.Where(i => i.BranchId == branchId.Value);

            return new ExpenseInvoiceStatisticsDto
            {
                TotalCount = await rows.CountAsync(ct),
                UnpaidCount = await rows.CountAsync(i =>
                    i.Status == ExpenseInvoiceStatus.Unpaid
                    || i.Status == ExpenseInvoiceStatus.PartiallyPaid, ct),
                OverdueCount = await rows.CountAsync(i =>
                    i.DueDate != null
                    && i.DueDate < today
                    && (i.Status == ExpenseInvoiceStatus.Unpaid
                        || i.Status == ExpenseInvoiceStatus.PartiallyPaid), ct),
                TotalAmount = await rows.SumAsync(i => (decimal?)i.TotalAmount, ct) ?? 0m,
                UnpaidAmount = await rows
                    .Where(i => i.Status == ExpenseInvoiceStatus.Unpaid
                             || i.Status == ExpenseInvoiceStatus.PartiallyPaid)
                    .SumAsync(i => (decimal?)(i.TotalAmount - i.PaidAmount), ct) ?? 0m,
                ThisMonthAmount = await rows
                    .Where(i => i.InvoiceDate >= monthStart)
                    .SumAsync(i => (decimal?)i.TotalAmount, ct) ?? 0m,
                CurrencyCode = defaultCurrency
            };
        }

        public Task<ExpenseInvoiceAttachment?> GetAttachmentAsync(Guid invoiceId, Guid attachmentId, CancellationToken ct = default)
            => _context.ExpenseInvoiceAttachments
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ExpenseInvoiceId == invoiceId, ct);

        public Task AddAttachmentAsync(ExpenseInvoiceAttachment attachment, CancellationToken ct = default)
            => _context.ExpenseInvoiceAttachments.AddAsync(attachment, ct).AsTask();

        public Task RemoveAttachmentAsync(ExpenseInvoiceAttachment attachment, CancellationToken ct = default)
        {
            _context.ExpenseInvoiceAttachments.Remove(attachment);
            return Task.CompletedTask;
        }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);

        // ────────────────────────── filtering ──────────────────────────────

        private static IQueryable<ExpenseInvoice> ApplyFilters(IQueryable<ExpenseInvoice> q, ExpenseInvoiceQueryDto f)
        {
            if (!string.IsNullOrWhiteSpace(f.InvoiceNumber))
            {
                var term = f.InvoiceNumber.Trim();
                q = q.Where(i => EF.Functions.ILike(i.InvoiceNumber, $"%{term}%"));
            }

            if (!string.IsNullOrWhiteSpace(f.Supplier))
            {
                var term = f.Supplier.Trim();
                q = q.Where(i => i.SupplierName != null && EF.Functions.ILike(i.SupplierName, $"%{term}%"));
            }

            if (f.SupplierId.HasValue)
                q = q.Where(i => i.SupplierId == f.SupplierId.Value);

            if (f.ExpenseCategoryId.HasValue)
                q = q.Where(i => i.ExpenseCategoryId == f.ExpenseCategoryId.Value);

            if (!string.IsNullOrWhiteSpace(f.Status)
                && Enum.TryParse<ExpenseInvoiceStatus>(f.Status, ignoreCase: true, out var status))
            {
                q = q.Where(i => i.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(f.PaymentMethod)
                && Enum.TryParse<ExpensePaymentMethod>(f.PaymentMethod, ignoreCase: true, out var method))
            {
                q = q.Where(i => i.PaymentMethod == method);
            }

            if (f.BranchId.HasValue)
                q = q.Where(i => i.BranchId == f.BranchId.Value);

            if (f.DateFrom.HasValue)
                q = q.Where(i => i.InvoiceDate >= f.DateFrom.Value.Date);

            if (f.DateTo.HasValue)
                q = q.Where(i => i.InvoiceDate < f.DateTo.Value.Date.AddDays(1));

            return q;
        }
    }
}
