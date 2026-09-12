using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs.ExpenseInvoice;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services.Storage;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Business logic for expense / supplier-bill invoices.
    ///
    /// Side-effect rules:
    ///   • Persisted total is always sanity-checked against components — bad
    ///     totals are corrected, not silently accepted, so reports stay honest.
    ///   • File writes and DB writes are paired such that an orphan never
    ///     remains on disk: on DB-save failure, the just-uploaded file is
    ///     scrubbed; on attachment delete, the DB row is removed first, then
    ///     the file — DB is the source of truth.
    /// </summary>
    public sealed class ExpenseInvoiceService : IExpenseInvoiceService
    {
        private readonly IExpenseInvoiceRepository _repo;
        private readonly IFileStorageService _storage;
        private readonly IHttpContextAccessor _http;
        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenantResolver;
        private readonly IBranchContext _branchContext;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly ILogger<ExpenseInvoiceService> _logger;

        private const string AttachmentScope = "expense-invoices";
        private const string ControllerRoute = "/api/expense-invoices";
        private const string DefaultCurrency = "USD";

        public ExpenseInvoiceService(
            IExpenseInvoiceRepository repo,
            IFileStorageService storage,
            IHttpContextAccessor http,
            PosDbContext context,
            ITenantResolver tenantResolver,
            IBranchContext branchContext,
            ICurrentUserAccessor currentUser,
            ILogger<ExpenseInvoiceService> logger)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Snapshots the category name into <see cref="ExpenseInvoice.CategoryLabel"/>
        /// when an <see cref="ExpenseInvoice.ExpenseCategoryId"/> is provided. The
        /// user's typed CategoryLabel wins when set — same override pattern as supplier.
        /// </summary>
        private async Task ApplyCategorySnapshotAsync(ExpenseInvoice invoice, CreateExpenseInvoiceDto dto, CancellationToken ct)
        {
            if (!dto.ExpenseCategoryId.HasValue) return;

            var category = await _context.ExpenseCategories
                .AsNoTracking()
                .Where(c => c.Id == dto.ExpenseCategoryId.Value)
                .Select(c => new { c.Name, c.NameAr })
                .FirstOrDefaultAsync(ct);

            if (category is null)
                throw new ValidationException($"Category '{dto.ExpenseCategoryId}' was not found.");

            invoice.CategoryLabel = string.IsNullOrWhiteSpace(dto.CategoryLabel)
                ? category.Name
                : dto.CategoryLabel.Trim();
        }

        /// <summary>
        /// Snapshots the supplier's display name into the invoice when a
        /// SupplierId is supplied. Leaves user-typed values intact when no
        /// SupplierId is set — that branch is the "manual one-off expense"
        /// path. Throws if the supplier id is unknown so bad client state
        /// surfaces immediately rather than as a silent dangling FK.
        /// </summary>
        private async Task ApplySupplierSnapshotAsync(ExpenseInvoice invoice, CreateExpenseInvoiceDto dto, CancellationToken ct)
        {
            if (!dto.SupplierId.HasValue) return;

            var supplier = await _context.Suppliers
                .AsNoTracking()
                .Where(s => s.Id == dto.SupplierId.Value)
                .Select(s => new { s.Name, s.NameAr, s.MobileNumber, s.TaxNumber })
                .FirstOrDefaultAsync(ct);

            if (supplier is null)
                throw new ValidationException($"Supplier '{dto.SupplierId}' was not found.");

            // Prefer the user-typed value if they explicitly overrode it; otherwise
            // pull the canonical name from the supplier so the list view is
            // populated without the user having to retype it.
            invoice.SupplierName = string.IsNullOrWhiteSpace(dto.SupplierName)
                ? supplier.Name
                : dto.SupplierName.Trim();
            invoice.SupplierPhone ??= supplier.MobileNumber;
            invoice.SupplierTaxNumber ??= supplier.TaxNumber;
        }

        // ───────────────────────── CREATE ──────────────────────────────────

        public async Task<ExpenseInvoiceDetailsDto> CreateAsync(
            CreateExpenseInvoiceDto dto,
            Guid? userId,
            string? userName,
            bool isArabic,
            CancellationToken ct)
        {
            ValidateAmounts(dto.Subtotal, dto.TaxAmount, dto.DiscountAmount, dto.TotalAmount);
            var requestedStatus = ParseEnum(dto.Status, ExpenseInvoiceStatus.Unpaid);
            var paidAmount = ExpenseInvoicePaymentPolicy.ResolvePaidAmount(
                dto.TotalAmount, dto.PaidAmount, requestedStatus);

            var invoiceNumber = (dto.InvoiceNumber ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(invoiceNumber))
                throw new ValidationException("Invoice number is required.");

            var branchId = await GetCurrentBranchIdAsync(ct);
            var invoice = new ExpenseInvoice
            {
                Id = Guid.NewGuid(),
                // Stamp TenantId explicitly — without this the row is invisible
                // to subsequent reads (the global query filter rejects it).
                TenantId = _tenantResolver.GetTenantId(),
                InvoiceNumber = invoiceNumber,
                SupplierName = string.IsNullOrWhiteSpace(dto.SupplierName) ? null : dto.SupplierName.Trim(),
                SupplierPhone = dto.SupplierPhone?.Trim(),
                SupplierTaxNumber = dto.SupplierTaxNumber?.Trim(),
                SupplierId = dto.SupplierId,
                ExpenseCategoryId = dto.ExpenseCategoryId,
                CategoryLabel = string.IsNullOrWhiteSpace(dto.CategoryLabel) ? null : dto.CategoryLabel.Trim(),
                InvoiceDate = EnsureUtc(dto.InvoiceDate),
                DueDate = dto.DueDate.HasValue ? EnsureUtc(dto.DueDate.Value) : null,
                Subtotal = dto.Subtotal,
                TaxAmount = dto.TaxAmount,
                DiscountAmount = dto.DiscountAmount,
                TotalAmount = dto.TotalAmount,
                PaidAmount = paidAmount,
                PurchaseOrderId = dto.PurchaseOrderId,
                CurrencyCode = NormaliseCurrency(dto.CurrencyCode),
                PaymentMethod = ParseEnum(dto.PaymentMethod, ExpensePaymentMethod.Unspecified),
                PaymentMethodDetail = dto.PaymentMethodDetail?.Trim(),
                Notes = dto.Notes?.Trim(),
                Status = ExpenseInvoicePaymentPolicy.ResolveStatus(
                    dto.TotalAmount, paidAmount, requestedStatus),
                BranchId = branchId,
                CreatedById = userId
            };

            await ApplySupplierSnapshotAsync(invoice, dto, ct);
            await ApplyCategorySnapshotAsync(invoice, dto, ct);
            await ValidatePurchaseOrderLinkAsync(invoice, excludeInvoiceId: null, ct);

            var auditAcknowledgement = await ValidateDuplicateAsync(
                invoice, dto.AcknowledgeDuplicateWarning, excludeId: null, ct);

            await _repo.AddAsync(invoice, ct);
            if (auditAcknowledgement)
                await AddDuplicateAcknowledgementAsync(invoice, userId, userName, ct);

            await _repo.SaveChangesAsync(ct);

            _logger.LogInformation("Expense invoice {InvoiceId} created (number={InvoiceNumber}, total={Total})",
                invoice.Id, invoice.InvoiceNumber, invoice.TotalAmount);

            return await GetDetailsAsync(invoice.Id, isArabic, ct);
        }

        // ───────────────────────── UPDATE ──────────────────────────────────

        public async Task<ExpenseInvoiceDetailsDto> UpdateAsync(
            Guid id,
            UpdateExpenseInvoiceDto dto,
            Guid? userId,
            string? userName,
            bool isArabic,
            CancellationToken ct)
        {
            var invoice = await _repo.GetTrackedByIdAsync(id, ct)
                ?? throw new NotFoundException(nameof(ExpenseInvoice), id);
            await EnsureCurrentBranchAsync(invoice, ct);
            EnsureNotShiftExpense(invoice);
            var previousKey = DuplicateKey.From(invoice);
            var previousPaidAmount = invoice.PaidAmount;
            var previousStatus = invoice.Status;

            ValidateAmounts(dto.Subtotal, dto.TaxAmount, dto.DiscountAmount, dto.TotalAmount);
            var requestedStatus = ParseEnum(dto.Status, ExpenseInvoiceStatus.Unpaid);
            var requestedPaidAmount = dto.PaidAmount
                ?? ResolveLegacyUpdatePaidAmount(invoice, dto.TotalAmount, requestedStatus);
            var paidAmount = ExpenseInvoicePaymentPolicy.ResolvePaidAmount(
                dto.TotalAmount, requestedPaidAmount, requestedStatus);

            var newNumber = (dto.InvoiceNumber ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(newNumber))
                throw new ValidationException("Invoice number is required.");

            invoice.InvoiceNumber = newNumber;
            invoice.SupplierName = string.IsNullOrWhiteSpace(dto.SupplierName) ? null : dto.SupplierName.Trim();
            invoice.SupplierPhone = dto.SupplierPhone?.Trim();
            invoice.SupplierTaxNumber = dto.SupplierTaxNumber?.Trim();
            invoice.SupplierId = dto.SupplierId;
            invoice.ExpenseCategoryId = dto.ExpenseCategoryId;
            invoice.CategoryLabel = dto.CategoryLabel?.Trim();
            invoice.InvoiceDate = EnsureUtc(dto.InvoiceDate);
            invoice.DueDate = dto.DueDate.HasValue ? EnsureUtc(dto.DueDate.Value) : null;
            invoice.Subtotal = dto.Subtotal;
            invoice.TaxAmount = dto.TaxAmount;
            invoice.DiscountAmount = dto.DiscountAmount;
            invoice.TotalAmount = dto.TotalAmount;
            invoice.PaidAmount = paidAmount;
            invoice.PurchaseOrderId = dto.PurchaseOrderId ?? invoice.PurchaseOrderId;
            invoice.CurrencyCode = NormaliseCurrency(dto.CurrencyCode);
            invoice.PaymentMethod = ParseEnum(dto.PaymentMethod, ExpensePaymentMethod.Unspecified);
            invoice.PaymentMethodDetail = dto.PaymentMethodDetail?.Trim();
            invoice.Notes = dto.Notes?.Trim();
            invoice.Status = ExpenseInvoicePaymentPolicy.ResolveStatus(
                dto.TotalAmount, paidAmount, requestedStatus);

            await ApplySupplierSnapshotAsync(invoice, dto, ct);
            await ApplyCategorySnapshotAsync(invoice, dto, ct);
            await ValidatePurchaseOrderLinkAsync(invoice, id, ct);

            var auditAcknowledgement = false;
            if (!previousKey.Equals(DuplicateKey.From(invoice)))
            {
                auditAcknowledgement = await ValidateDuplicateAsync(
                    invoice, dto.AcknowledgeDuplicateWarning, id, ct);
            }

            if (auditAcknowledgement)
                await AddDuplicateAcknowledgementAsync(invoice, userId, userName, ct);

            if (invoice.PaidAmount != previousPaidAmount || invoice.Status != previousStatus)
                await AddPaymentAuditAsync(invoice, previousPaidAmount, previousStatus, ct);

            await _repo.SaveChangesAsync(ct);
            return await GetDetailsAsync(id, isArabic, ct);
        }

        public async Task<ExpenseInvoiceDetailsDto> UpdatePaymentAsync(
            Guid id,
            UpdateExpenseInvoicePaymentDto dto,
            bool isArabic,
            CancellationToken ct)
        {
            var invoice = await _repo.GetTrackedByIdAsync(id, ct)
                ?? throw new NotFoundException(nameof(ExpenseInvoice), id);
            await EnsureCurrentBranchAsync(invoice, ct);
            EnsureNotShiftExpense(invoice);

            var previousPaidAmount = invoice.PaidAmount;
            var previousStatus = invoice.Status;
            invoice.PaidAmount = ExpenseInvoicePaymentPolicy.ResolvePaidAmount(
                invoice.TotalAmount, dto.PaidAmount, invoice.Status);
            invoice.Status = ExpenseInvoicePaymentPolicy.ResolveStatus(
                invoice.TotalAmount, invoice.PaidAmount, ExpenseInvoiceStatus.Unpaid);

            if (!string.IsNullOrWhiteSpace(dto.PaymentMethod))
                invoice.PaymentMethod = ParseEnum(dto.PaymentMethod, invoice.PaymentMethod);

            if (dto.PaymentMethodDetail is not null)
                invoice.PaymentMethodDetail = dto.PaymentMethodDetail.Trim();

            await AddPaymentAuditAsync(
                invoice,
                previousPaidAmount,
                previousStatus,
                ct);
            await _repo.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Expense invoice {InvoiceId} payment updated: paid={PaidAmount}, status={Status}",
                invoice.Id,
                invoice.PaidAmount,
                invoice.Status);

            return await GetDetailsAsync(id, isArabic, ct);
        }

        // ─────────────────────── SOFT DELETE ───────────────────────────────

        public async Task SoftDeleteAsync(Guid id, CancellationToken ct)
        {
            var invoice = await _repo.GetTrackedByIdAsync(id, ct)
                ?? throw new NotFoundException(nameof(ExpenseInvoice), id);
            await EnsureCurrentBranchAsync(invoice, ct);
            EnsureNotShiftExpense(invoice);

            invoice.DeletedAt = DateTime.UtcNow;
            await _repo.SaveChangesAsync(ct);

            _logger.LogInformation("Expense invoice {InvoiceId} soft-deleted", id);
        }

        // ───────────────────────── READS ──────────────────────────────────

        public async Task<ExpenseInvoiceDetailsDto> GetDetailsAsync(Guid id, bool isArabic, CancellationToken ct)
        {
            var dto = await _repo.GetDetailsAsync(id, isArabic, _storage.GetPublicUrl, ControllerRoute, ct)
                ?? throw new NotFoundException(nameof(ExpenseInvoice), id);
            if (dto.BranchId != await GetCurrentBranchIdAsync(ct))
                throw new NotFoundException(nameof(ExpenseInvoice), id);
            return dto;
        }

        public async Task<PaginatedResponse<ExpenseInvoiceListItemDto>> GetPagedAsync(ExpenseInvoiceQueryDto query, bool isArabic, CancellationToken ct)
        {
            query.BranchId = await ResolveReadBranchIdAsync(query.BranchId, ct);
            return await _repo.GetPagedAsync(query, isArabic, _storage.GetPublicUrl, ct);
        }

        public async Task<ExpenseInvoiceStatisticsDto> GetStatisticsAsync(Guid? branchId, CancellationToken ct)
            => await _repo.GetStatisticsAsync(DefaultCurrency, await ResolveReadBranchIdAsync(branchId, ct), ct);

        // ───────────────────────── ATTACHMENTS ─────────────────────────────

        public async Task<ExpenseInvoiceAttachmentDto> AddAttachmentAsync(Guid invoiceId, IFormFile file, Guid? userId, CancellationToken ct)
        {
            var invoice = await _repo.GetTrackedByIdAsync(invoiceId, ct)
                ?? throw new NotFoundException(nameof(ExpenseInvoice), invoiceId);
            await EnsureCurrentBranchAsync(invoice, ct);

            var stored = await _storage.SaveAsync(file, AttachmentScope, ct);

            try
            {
                var attachment = new ExpenseInvoiceAttachment
                {
                    Id = Guid.NewGuid(),
                    // Same tenant as the parent invoice — keeps the query filter
                    // consistent and the row visible after save.
                    TenantId = invoice.TenantId,
                    ExpenseInvoiceId = invoiceId,
                    OriginalFileName = stored.OriginalFileName,
                    StoredFileName = stored.StoredFileName,
                    FileExtension = stored.FileExtension,
                    FileSize = stored.FileSize,
                    MimeType = stored.MimeType,
                    RelativePath = stored.RelativePath,
                    FullUrl = stored.PublicUrl,
                    UploadedAt = DateTime.UtcNow,
                    UploadedById = userId,
                    IsPrimary = !invoice.Attachments.Any()
                };

                await _repo.AddAttachmentAsync(attachment, ct);
                await _repo.SaveChangesAsync(ct);

                return new ExpenseInvoiceAttachmentDto
                {
                    Id = attachment.Id,
                    ExpenseInvoiceId = invoiceId,
                    OriginalFileName = attachment.OriginalFileName,
                    FileExtension = attachment.FileExtension,
                    FileSize = attachment.FileSize,
                    MimeType = attachment.MimeType,
                    Url = _storage.GetPublicUrl(attachment.RelativePath) ?? string.Empty,
                    DownloadUrl = $"{ControllerRoute}/{invoiceId}/attachments/{attachment.Id}/download",
                    UploadedAt = attachment.UploadedAt,
                    UploadedByName = null,
                    IsPrimary = attachment.IsPrimary
                };
            }
            catch
            {
                // Roll back the just-written file so disk and DB stay in sync.
                await _storage.DeleteAsync(stored.RelativePath, CancellationToken.None);
                throw;
            }
        }

        public async Task DeleteAttachmentAsync(Guid invoiceId, Guid attachmentId, CancellationToken ct)
        {
            var attachment = await _repo.GetAttachmentAsync(invoiceId, attachmentId, ct)
                ?? throw new NotFoundException("ExpenseInvoiceAttachment", attachmentId);
            var invoice = await _repo.GetTrackedByIdAsync(invoiceId, ct)
                ?? throw new NotFoundException(nameof(ExpenseInvoice), invoiceId);
            await EnsureCurrentBranchAsync(invoice, ct);

            var relativePath = attachment.RelativePath;
            await _repo.RemoveAttachmentAsync(attachment, ct);
            await _repo.SaveChangesAsync(ct);

            // Physical cleanup is best-effort — never fail the request because
            // the file is already orphaned in disk-cleanup terms.
            await _storage.DeleteAsync(relativePath, CancellationToken.None);
        }

        public async Task<(Stream Stream, string FileName, string MimeType)> OpenAttachmentForDownloadAsync(
            Guid invoiceId, Guid attachmentId, CancellationToken ct)
        {
            var attachment = await _repo.GetAttachmentAsync(invoiceId, attachmentId, ct)
                ?? throw new NotFoundException("ExpenseInvoiceAttachment", attachmentId);
            var invoice = await _repo.GetTrackedByIdAsync(invoiceId, ct)
                ?? throw new NotFoundException(nameof(ExpenseInvoice), invoiceId);
            await EnsureCurrentBranchAsync(invoice, ct);

            Stream stream;
            try
            {
                stream = await _storage.OpenReadAsync(attachment.RelativePath, ct);
            }
            catch (FileNotFoundException)
            {
                throw new NotFoundException("Attachment file is missing.");
            }

            return (stream, attachment.OriginalFileName, attachment.MimeType);
        }

        // ───────────────────────── helpers ─────────────────────────────────

        private static void ValidateAmounts(decimal subtotal, decimal tax, decimal discount, decimal total)
        {
            if (subtotal < 0 || tax < 0 || discount < 0 || total < 0)
                throw new ValidationException("Amounts must be non-negative.");

            // Sanity check: |computed - declared| > 0.01 is treated as user error
            // so silent rounding never masks a typo. Reporting integrity > UX.
            var computed = subtotal + tax - discount;
            if (Math.Abs(computed - total) > 0.01m)
                throw new ValidationException(
                    $"Total ({total:0.00}) does not match subtotal + tax − discount ({computed:0.00}).");
        }

        private async Task ValidatePurchaseOrderLinkAsync(
            ExpenseInvoice invoice,
            Guid? excludeInvoiceId,
            CancellationToken ct)
        {
            if (!invoice.PurchaseOrderId.HasValue)
                return;

            var purchaseOrder = await _context.PurchaseOrders
                .AsNoTracking()
                .Where(order => order.Id == invoice.PurchaseOrderId.Value)
                .Select(order => new { order.BranchId, order.SupplierId })
                .FirstOrDefaultAsync(ct)
                ?? throw new ValidationException("The related purchase order was not found.");

            if (purchaseOrder.BranchId != invoice.BranchId)
                throw new ValidationException("The related purchase order belongs to another branch.");

            if (invoice.SupplierId.HasValue && purchaseOrder.SupplierId != invoice.SupplierId.Value)
                throw new ValidationException("The invoice supplier must match the purchase order supplier.");

            var alreadyLinked = await _context.ExpenseInvoices
                .AsNoTracking()
                .AnyAsync(existing =>
                    existing.PurchaseOrderId == invoice.PurchaseOrderId
                    && (!excludeInvoiceId.HasValue || existing.Id != excludeInvoiceId.Value),
                    ct);

            if (alreadyLinked)
                throw new ValidationException("The purchase order already has a supplier invoice.");
        }

        private static string NormaliseCurrency(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return DefaultCurrency;
            var trimmed = raw.Trim().ToUpperInvariant();
            return trimmed.Length == 3 ? trimmed : DefaultCurrency;
        }

        private static decimal ResolveLegacyUpdatePaidAmount(
            ExpenseInvoice invoice,
            decimal totalAmount,
            ExpenseInvoiceStatus requestedStatus)
            => requestedStatus switch
            {
                ExpenseInvoiceStatus.Paid => totalAmount,
                ExpenseInvoiceStatus.Unpaid => 0m,
                _ => invoice.PaidAmount
            };

        private Task AddPaymentAuditAsync(
            ExpenseInvoice invoice,
            decimal previousPaidAmount,
            ExpenseInvoiceStatus previousStatus,
            CancellationToken ct)
        {
            var occurredAt = DateTime.UtcNow;
            return _repo.AddAuditLogAsync(new ExpenseInvoiceAuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = invoice.TenantId,
                ExpenseInvoiceId = invoice.Id,
                EventType = ExpenseInvoiceAuditEventType.PaymentUpdated,
                ActorUserId = _currentUser.UserIdOrNull,
                InvoiceNumber = invoice.InvoiceNumber,
                SupplierId = invoice.SupplierId,
                SupplierName = invoice.SupplierName,
                InvoiceDate = invoice.InvoiceDate,
                AcknowledgedAt = occurredAt,
                Reason = $"Paid amount {previousPaidAmount:0.00} -> {invoice.PaidAmount:0.00}; status {previousStatus} -> {invoice.Status}.",
                CreatedAt = occurredAt,
                UpdatedAt = occurredAt
            }, ct);
        }

        private static TEnum ParseEnum<TEnum>(string? raw, TEnum fallback) where TEnum : struct, Enum
            => Enum.TryParse<TEnum>(raw, ignoreCase: true, out var parsed) ? parsed : fallback;

        private static DateTime EnsureUtc(DateTime value)
            => value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };

        private async Task<bool> ValidateDuplicateAsync(
            ExpenseInvoice invoice,
            bool acknowledged,
            Guid? excludeId,
            CancellationToken ct)
        {
            var behavior = await _repo.GetDuplicateInvoiceBehaviorAsync(invoice.TenantId, ct);
            if (behavior == DuplicateInvoiceBehavior.Disabled)
                return false;

            var duplicateExists = await _repo.PotentialDuplicateExistsAsync(
                invoice.BranchId,
                invoice.SupplierId,
                invoice.SupplierName,
                invoice.InvoiceNumber,
                invoice.InvoiceDate,
                excludeId,
                ct);

            var disposition = ExpenseInvoiceDuplicatePolicy.Evaluate(
                behavior, duplicateExists, acknowledged);
            return ResolveDuplicateDisposition(disposition, behavior, invoice);
        }

        private static bool ResolveDuplicateDisposition(
            ExpenseInvoiceDuplicateDisposition disposition,
            DuplicateInvoiceBehavior behavior,
            ExpenseInvoice invoice)
        {
            if (disposition == ExpenseInvoiceDuplicateDisposition.Save)
                return false;
            if (disposition == ExpenseInvoiceDuplicateDisposition.SaveWithAcknowledgementAudit)
                return true;

            throw new DuplicateExpenseInvoiceException(new ExpenseInvoiceDuplicateWarningDto
            {
                CanOverride = disposition == ExpenseInvoiceDuplicateDisposition.RequireAcknowledgement,
                Behavior = behavior.ToString(),
                InvoiceNumber = invoice.InvoiceNumber,
                SupplierName = invoice.SupplierName,
                InvoiceDate = invoice.InvoiceDate
            });
        }

        private Task AddDuplicateAcknowledgementAsync(
            ExpenseInvoice invoice,
            Guid? userId,
            string? userName,
            CancellationToken ct)
        {
            var acknowledgedAt = DateTime.UtcNow;
            return _repo.AddAuditLogAsync(new ExpenseInvoiceAuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = invoice.TenantId,
                ExpenseInvoiceId = invoice.Id,
                EventType = ExpenseInvoiceAuditEventType.DuplicateWarningAcknowledged,
                ActorUserId = userId,
                ActorUserName = string.IsNullOrWhiteSpace(userName) ? null : userName.Trim(),
                InvoiceNumber = invoice.InvoiceNumber,
                SupplierId = invoice.SupplierId,
                SupplierName = invoice.SupplierName,
                InvoiceDate = invoice.InvoiceDate,
                AcknowledgedAt = acknowledgedAt,
                CreatedAt = acknowledgedAt,
                UpdatedAt = acknowledgedAt
            }, ct);
        }

        private async Task<Guid> GetCurrentBranchIdAsync(CancellationToken ct)
            => (await _branchContext.GetCurrentAsync(ct)).CurrentBranch.Id;

        private async Task<Guid?> ResolveReadBranchIdAsync(Guid? requestedBranchId, CancellationToken ct)
        {
            if (requestedBranchId == Guid.Empty)
            {
                if (!_currentUser.IsAdminOrManager)
                    throw new ForbiddenException("All branches expense-invoice scope requires administrator or manager access.");
                return null;
            }

            var context = await _branchContext.GetCurrentAsync(ct);
            if (!requestedBranchId.HasValue)
                return context.CurrentBranch.Id;

            var selected = context.AssignedBranches.FirstOrDefault(b => b.Id == requestedBranchId.Value);
            if (selected is null)
                throw new ForbiddenException("You are not assigned to the selected branch.");
            if (!selected.IsActive)
                throw new ValidationException("Inactive branches cannot be used as expense-invoice scope.");

            return requestedBranchId.Value;
        }

        /// <summary>
        /// A shift-linked expense is part of a drawer reconciliation. Editing the
        /// amount or soft-deleting it from the back office would silently rewrite
        /// that shift's expected cash, so those paths stay closed — the shift screen
        /// owns them (record + audited void).
        /// </summary>
        private static void EnsureNotShiftExpense(ExpenseInvoice invoice)
        {
            if (invoice.CashierShiftId.HasValue)
                throw new ValidationException(
                    "Cashier shift expenses can only be managed from the shift screen.");
        }

        private async Task EnsureCurrentBranchAsync(ExpenseInvoice invoice, CancellationToken ct)
        {
            var branchId = await GetCurrentBranchIdAsync(ct);
            if (invoice.BranchId != branchId)
                throw new NotFoundException(nameof(ExpenseInvoice), invoice.Id);
        }

        private sealed record DuplicateKey(
            Guid? SupplierId,
            string? ManualSupplierName,
            string InvoiceNumber,
            DateTime InvoiceDate)
        {
            public static DuplicateKey From(ExpenseInvoice invoice) => new(
                invoice.SupplierId,
                invoice.SupplierId.HasValue ? null : invoice.SupplierName,
                invoice.InvoiceNumber,
                invoice.InvoiceDate.Date);
        }
    }
}
