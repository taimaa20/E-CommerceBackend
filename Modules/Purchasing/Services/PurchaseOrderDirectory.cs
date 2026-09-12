using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Purchasing.DTOs;
using RestaurantPos.Api.Modules.Purchasing.Security;
using RestaurantPos.Api.Modules.Retail.Domain;

namespace RestaurantPos.Api.Modules.Purchasing.Services
{
    /// <inheritdoc cref="IPurchaseOrderDirectory"/>
    public sealed class PurchaseOrderDirectory : IPurchaseOrderDirectory
    {
        private const int MaxPageSize = 100;

        /// <summary>
        /// Merge-paging reads (offset + pageSize) rows from each source before combining them,
        /// so the cost grows with how deep the caller pages. Bounded here rather than left to
        /// grow: past this depth the answer is a filter, not a bigger read.
        /// </summary>
        private const int MaxMergeWindow = 2_000;

        private readonly PosDbContext _context;

        public PurchaseOrderDirectory(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<PaginatedResponse<PurchaseOrderRowDto>> GetPageAsync(
            Guid branchId,
            PurchaseOrderDirectoryQuery query,
            PurchasingVisibility visibility,
            bool isArabic,
            CancellationToken ct = default)
        {
            var pageNumber = Math.Max(query.PageNumber, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
            var offset = (pageNumber - 1) * pageSize;

            if (offset + pageSize > MaxMergeWindow)
                throw new ValidationException(
                    "Too many purchase orders to page this far. Narrow the list with a search, a type or a stage first.");

            var paymentStatus = ParsePaymentStatus(query.PaymentStatus);

            var wantsRaw = visibility.IncludeRawMaterials
                && KindWanted(query.ItemKind, PurchaseItemKinds.RawMaterial);
            var wantsFinished = visibility.IncludeFinishedProducts
                && KindWanted(query.ItemKind, PurchaseItemKinds.FinishedProduct)
                // Only a raw-material purchase carries a supplier invoice, so a payment filter
                // has nothing to match on the finished-goods side.
                && paymentStatus is null;

            var window = offset + pageSize;
            var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
            var stage = NormaliseStage(query.Stage);

            var (rawRows, rawCount) = wantsRaw
                ? await ReadRawMaterialOrdersAsync(branchId, search, stage, paymentStatus, window, isArabic, ct)
                : (new List<MergeRow>(), 0);

            var (finishedRows, finishedCount) = wantsFinished
                ? await ReadFinishedGoodsOrdersAsync(branchId, search, stage, window, isArabic, ct)
                : (new List<MergeRow>(), 0);

            var merged = rawRows
                .Concat(finishedRows)
                .OrderByDescending(row => row.SortDate)
                .ThenByDescending(row => row.CreatedAt)
                .Skip(offset)
                .Take(pageSize)
                .Select(row => row.Row)
                .ToList();

            return new PaginatedResponse<PurchaseOrderRowDto>
            {
                Items = merged,
                TotalCount = rawCount + finishedCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        private async Task<(List<MergeRow> Rows, int Count)> ReadRawMaterialOrdersAsync(
            Guid branchId,
            string? search,
            string? stage,
            ExpenseInvoiceStatus? paymentStatus,
            int window,
            bool isArabic,
            CancellationToken ct)
        {
            var statuses = RawStatusesForStage(stage);
            if (statuses is { Count: 0 })
                return (new List<MergeRow>(), 0);

            var orders = _context.PurchaseOrders
                .AsNoTracking()
                .Where(po => po.BranchId == branchId);

            if (statuses is not null)
                orders = orders.Where(po => statuses.Contains(po.Status));

            // Matches the existing procurement list: an order with no invoice reads as unpaid.
            if (paymentStatus == ExpenseInvoiceStatus.Unpaid)
                orders = orders.Where(po =>
                    po.SupplierInvoice == null
                    || po.SupplierInvoice.Status == ExpenseInvoiceStatus.Unpaid);
            else if (paymentStatus.HasValue)
                orders = orders.Where(po =>
                    po.SupplierInvoice != null
                    && po.SupplierInvoice.Status == paymentStatus.Value);

            if (search is not null)
            {
                var pattern = $"%{search}%";
                orders = orders.Where(po =>
                    EF.Functions.ILike(po.OrderNumber, pattern)
                    || (po.InvoiceNumber != null && EF.Functions.ILike(po.InvoiceNumber, pattern))
                    || (po.SupplierInvoice != null && EF.Functions.ILike(po.SupplierInvoice.InvoiceNumber, pattern))
                    || (po.Supplier != null && EF.Functions.ILike(po.Supplier.Name, pattern)));
            }

            var count = await orders.CountAsync(ct);

            var projected = await orders
                .OrderByDescending(po => po.SupplierInvoice != null ? po.SupplierInvoice.InvoiceDate : po.ExpectedDate)
                .ThenByDescending(po => po.CreatedAt)
                .Take(window)
                .Select(po => new
                {
                    po.Id,
                    po.OrderNumber,
                    po.InvoiceNumber,
                    po.SupplierId,
                    SupplierName = po.Supplier != null ? po.Supplier.Name : null,
                    SupplierNameAr = po.Supplier != null ? po.Supplier.NameAr : null,
                    po.Status,
                    po.ExpectedDate,
                    po.CreatedAt,
                    LineCount = po.Items.Count,
                    po.TotalAmount,
                    InvoiceId = (Guid?) (po.SupplierInvoice != null ? po.SupplierInvoice.Id : null),
                    InvoiceNumberFromInvoice = po.SupplierInvoice != null ? po.SupplierInvoice.InvoiceNumber : null,
                    InvoiceDate = (DateTime?) (po.SupplierInvoice != null ? po.SupplierInvoice.InvoiceDate : null),
                    InvoiceTotal = (decimal?) (po.SupplierInvoice != null ? po.SupplierInvoice.TotalAmount : null),
                    InvoicePaid = (decimal?) (po.SupplierInvoice != null ? po.SupplierInvoice.PaidAmount : null),
                    InvoiceStatus = (ExpenseInvoiceStatus?) (po.SupplierInvoice != null ? po.SupplierInvoice.Status : null)
                })
                .ToListAsync(ct);

            var rows = projected.Select(po =>
            {
                var total = po.InvoiceTotal ?? po.TotalAmount;
                var paid = po.InvoicePaid ?? 0m;
                var documentDate = po.InvoiceDate ?? po.ExpectedDate;

                return new MergeRow(
                    new PurchaseOrderRowDto
                    {
                        Id = po.Id,
                        ItemKind = PurchaseItemKinds.RawMaterial,
                        DocumentNumber = po.OrderNumber,
                        InvoiceNumber = po.InvoiceNumberFromInvoice ?? po.InvoiceNumber,
                        SupplierId = po.SupplierId,
                        SupplierDisplayName = GeneralHelper.ResolveDisplayName(
                            po.SupplierName ?? string.Empty, po.SupplierNameAr, isArabic),
                        DocumentDateUtc = documentDate,
                        LineCount = po.LineCount,
                        TotalAmount = total,
                        PaidAmount = paid,
                        Balance = total - paid,
                        PaymentStatus = ResolvePaymentStatus(po.InvoiceStatus),
                        Stage = RawStage(po.Status),
                        StockPosted = po.Status == PurchaseOrderStatus.Approved,
                        InvoiceId = po.InvoiceId,
                        CanSubmit = false,
                        CanReceive = po.Status == PurchaseOrderStatus.Draft,
                        CanApprove = po.Status == PurchaseOrderStatus.Received,
                        CanCancel = false
                    },
                    documentDate,
                    po.CreatedAt);
            }).ToList();

            return (rows, count);
        }

        private async Task<(List<MergeRow> Rows, int Count)> ReadFinishedGoodsOrdersAsync(
            Guid branchId,
            string? search,
            string? stage,
            int window,
            bool isArabic,
            CancellationToken ct)
        {
            var statuses = FinishedStatusesForStage(stage);
            if (statuses is { Count: 0 })
                return (new List<MergeRow>(), 0);

            var orders = _context.RetailPurchaseOrders
                .AsNoTracking()
                .Where(o => o.BranchId == branchId);

            if (statuses is not null)
                orders = orders.Where(o => statuses.Contains(o.Status));

            if (search is not null)
            {
                var pattern = $"%{search}%";
                orders = orders.Where(o =>
                    EF.Functions.ILike(o.OrderNumber, pattern)
                    || (o.InvoiceNumber != null && EF.Functions.ILike(o.InvoiceNumber, pattern))
                    || (o.Supplier != null && EF.Functions.ILike(o.Supplier.Name, pattern)));
            }

            var count = await orders.CountAsync(ct);

            var projected = await orders
                .OrderByDescending(o => o.OrderDateUtc)
                .ThenByDescending(o => o.CreatedAt)
                .Take(window)
                .Select(o => new
                {
                    o.Id,
                    o.OrderNumber,
                    o.InvoiceNumber,
                    o.SupplierId,
                    SupplierName = o.Supplier != null ? o.Supplier.Name : null,
                    SupplierNameAr = o.Supplier != null ? o.Supplier.NameAr : null,
                    o.Status,
                    o.OrderDateUtc,
                    o.CreatedAt,
                    LineCount = o.Lines.Count,
                    o.TotalCost,
                    o.StockPostedAtUtc
                })
                .ToListAsync(ct);

            var rows = projected.Select(o => new MergeRow(
                new PurchaseOrderRowDto
                {
                    Id = o.Id,
                    ItemKind = PurchaseItemKinds.FinishedProduct,
                    DocumentNumber = o.OrderNumber,
                    InvoiceNumber = o.InvoiceNumber,
                    SupplierId = o.SupplierId,
                    SupplierDisplayName = GeneralHelper.ResolveDisplayName(
                        o.SupplierName ?? string.Empty, o.SupplierNameAr, isArabic),
                    DocumentDateUtc = o.OrderDateUtc,
                    LineCount = o.LineCount,
                    TotalAmount = o.TotalCost,
                    // A finished-goods order does not raise a supplier invoice, so it reports the
                    // balance it actually has rather than borrowing the other model.
                    PaidAmount = 0m,
                    Balance = o.TotalCost,
                    PaymentStatus = nameof(ExpenseInvoiceStatus.Unpaid),
                    Stage = FinishedStage(o.Status),
                    StockPosted = o.StockPostedAtUtc.HasValue,
                    InvoiceId = null,
                    CanSubmit = o.Status == RetailPurchaseOrderStatus.Draft,
                    CanReceive = o.Status is RetailPurchaseOrderStatus.Draft or RetailPurchaseOrderStatus.Submitted,
                    CanApprove = false,
                    CanCancel = o.Status is RetailPurchaseOrderStatus.Draft or RetailPurchaseOrderStatus.Submitted
                },
                o.OrderDateUtc,
                o.CreatedAt)).ToList();

            return (rows, count);
        }

        private static bool KindWanted(string? requested, string kind)
            => string.IsNullOrWhiteSpace(requested)
               || string.Equals(requested, kind, StringComparison.OrdinalIgnoreCase);

        private static string? NormaliseStage(string? stage)
            => string.IsNullOrWhiteSpace(stage) ? null : stage.Trim();

        private static ExpenseInvoiceStatus? ParsePaymentStatus(string? value)
            => Enum.TryParse<ExpenseInvoiceStatus>(value, ignoreCase: true, out var parsed) ? parsed : null;

        /// <summary>Null means every stage; an empty list means this source has no such stage.</summary>
        private static List<PurchaseOrderStatus>? RawStatusesForStage(string? stage) => stage switch
        {
            null => null,
            PurchaseOrderStages.Draft => new() { PurchaseOrderStatus.Draft },
            PurchaseOrderStages.Received => new() { PurchaseOrderStatus.Received },
            PurchaseOrderStages.Completed => new() { PurchaseOrderStatus.Approved },
            PurchaseOrderStages.Cancelled => new() { PurchaseOrderStatus.Cancelled },
            _ => new()
        };

        private static List<RetailPurchaseOrderStatus>? FinishedStatusesForStage(string? stage) => stage switch
        {
            null => null,
            PurchaseOrderStages.Draft => new() { RetailPurchaseOrderStatus.Draft },
            PurchaseOrderStages.Ordered => new() { RetailPurchaseOrderStatus.Submitted },
            PurchaseOrderStages.Completed => new() { RetailPurchaseOrderStatus.Received },
            PurchaseOrderStages.Cancelled => new() { RetailPurchaseOrderStatus.Cancelled },
            _ => new()
        };

        private static string RawStage(PurchaseOrderStatus status) => status switch
        {
            PurchaseOrderStatus.Draft => PurchaseOrderStages.Draft,
            // Batches exist but stock is only updated on approval, so this is not Completed.
            PurchaseOrderStatus.Received => PurchaseOrderStages.Received,
            PurchaseOrderStatus.Approved => PurchaseOrderStages.Completed,
            _ => PurchaseOrderStages.Cancelled
        };

        private static string FinishedStage(RetailPurchaseOrderStatus status) => status switch
        {
            RetailPurchaseOrderStatus.Draft => PurchaseOrderStages.Draft,
            RetailPurchaseOrderStatus.Submitted => PurchaseOrderStages.Ordered,
            // The receipt is what posts the movements, so a received order is stock in hand.
            RetailPurchaseOrderStatus.Received => PurchaseOrderStages.Completed,
            _ => PurchaseOrderStages.Cancelled
        };

        private static string ResolvePaymentStatus(ExpenseInvoiceStatus? status) => status switch
        {
            ExpenseInvoiceStatus.PartiallyPaid => nameof(ExpenseInvoiceStatus.PartiallyPaid),
            ExpenseInvoiceStatus.Paid => nameof(ExpenseInvoiceStatus.Paid),
            _ => nameof(ExpenseInvoiceStatus.Unpaid)
        };

        /// <summary>Carries the sort keys alongside the row so the merge stays stable.</summary>
        private readonly record struct MergeRow(PurchaseOrderRowDto Row, DateTime SortDate, DateTime CreatedAt);
    }
}
