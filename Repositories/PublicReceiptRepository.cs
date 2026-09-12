using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public interface IPublicReceiptRepository
    {
        Task<PublicReceiptOrderSeed?> GetOrderSeedAsync(Guid orderId, Guid tenantId, CancellationToken ct);
        Task<PublicReceiptToken?> GetActiveTokenForOrderAsync(Guid orderId, Guid tenantId, DateTime nowUtc, CancellationToken ct);
        Task<bool> TokenExistsAsync(string token, CancellationToken ct);
        Task AddTokenAsync(PublicReceiptToken token, CancellationToken ct);
        Task SaveChangesAsync(CancellationToken ct);
        Task<PublicReceiptDto?> GetReceiptAsync(string token, bool isArabic, DateTime nowUtc, CancellationToken ct);
    }

    public sealed class PublicReceiptOrderSeed
    {
        public Guid OrderId { get; init; }
        public Guid TenantId { get; init; }
        public string? CustomerPhone { get; init; }
    }

    public sealed class PublicReceiptRepository : IPublicReceiptRepository
    {
        private const string TakeawayTableName = "TAKEAWAY";
        private const string DefaultRestaurantName = "Store";
        private const string DefaultCurrency = "JOD";

        private readonly PosDbContext _context;

        public PublicReceiptRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task<PublicReceiptOrderSeed?> GetOrderSeedAsync(Guid orderId, Guid tenantId, CancellationToken ct)
        {
            return _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == orderId && o.TenantId == tenantId)
                .Select(o => new PublicReceiptOrderSeed
                {
                    OrderId = o.Id,
                    TenantId = o.TenantId,
                    CustomerPhone = o.CustomerPhone ?? (o.Customer != null ? o.Customer.PhoneNumber : null)
                })
                .FirstOrDefaultAsync(ct);
        }

        public Task<PublicReceiptToken?> GetActiveTokenForOrderAsync(
            Guid orderId,
            Guid tenantId,
            DateTime nowUtc,
            CancellationToken ct)
        {
            return _context.PublicReceiptTokens
                .FirstOrDefaultAsync(t =>
                    t.OrderId == orderId &&
                    t.TenantId == tenantId &&
                    t.IsActive &&
                    (t.ExpireAtUtc == null || t.ExpireAtUtc > nowUtc), ct);
        }

        public Task<bool> TokenExistsAsync(string token, CancellationToken ct)
        {
            return _context.PublicReceiptTokens.AnyAsync(t => t.Token == token, ct);
        }

        public async Task AddTokenAsync(PublicReceiptToken token, CancellationToken ct)
        {
            await _context.PublicReceiptTokens.AddAsync(token, ct);
        }

        public Task SaveChangesAsync(CancellationToken ct)
        {
            return _context.SaveChangesAsync(ct);
        }

        public async Task<PublicReceiptDto?> GetReceiptAsync(
            string token,
            bool isArabic,
            DateTime nowUtc,
            CancellationToken ct)
        {
            var tokenRow = await _context.PublicReceiptTokens
                .AsNoTracking()
                .Where(t =>
                    t.Token == token &&
                    t.IsActive &&
                    (t.ExpireAtUtc == null || t.ExpireAtUtc > nowUtc))
                .Select(t => new { t.OrderId, t.TenantId })
                .FirstOrDefaultAsync(ct);

            if (tokenRow == null)
                return null;

            var settings = await GetReceiptSettingsAsync(tokenRow.TenantId, ct);
            var receipt = await BuildReceiptQuery(tokenRow.OrderId, tokenRow.TenantId, isArabic)
                .FirstOrDefaultAsync(ct);

            if (receipt == null)
                return null;

            var logoUrl = settings?.ShowLogoOnReceipt == false
                ? null
                : await GetTenantLogoUrlAsync(tokenRow.TenantId, ct);

            receipt.RemainingAmount = OrderPaymentHelper.RoundCurrency(Math.Max(0m, receipt.TotalAmount - receipt.PaidAmount));
            if (receipt.OrderSource == OrderSource.Online.ToString() &&
                !string.IsNullOrWhiteSpace(receipt.PaymentMethod) &&
                OrderPaymentHelper.IsCashMethod(receipt.PaymentMethod))
                receipt.PaymentMethod = isArabic
                    ? OrderPaymentHelper.CashLabelAr
                    : OrderPaymentHelper.CashLabelEn;

            receipt.PaymentStatus = receipt.IsRefunded
                ? OrderPaymentHelper.RefundedStatus
                : receipt.PaymentStatus == OrderPaymentHelper.FailedStatus
                    ? OrderPaymentHelper.FailedStatus
                    : receipt.PaidAmount >= receipt.TotalAmount || receipt.PaidAtUtc.HasValue
                        ? OrderPaymentHelper.PaidStatus
                        : receipt.PaidAmount > 0m
                            ? OrderPaymentHelper.PartialStatus
                            : OrderPaymentHelper.PendingStatus;
            receipt.Restaurant = MapRestaurant(settings, logoUrl);
            return receipt;
        }

        private IQueryable<PublicReceiptDto> BuildReceiptQuery(Guid orderId, Guid tenantId, bool isArabic)
        {
            return _context.Orders
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(o => o.Id == orderId && o.TenantId == tenantId && o.DeletedAt == null)
                .Select(o => new PublicReceiptDto
                {
                    OrderNumber = o.DisplayOrderNumber ?? o.PublicOrderNumber ?? o.OrderNumber,
                    TableName = o.TableId == null && o.OrderType == OrderType.Takeaway
                        ? TakeawayTableName
                        : o.Table != null && o.Table.DeletedAt == null
                            ? isArabic && o.Table.NameAr != null && o.Table.NameAr != string.Empty ? o.Table.NameAr : o.Table.Name
                            : o.TableName,
                    OrderType = o.OrderType.ToString(),
                    OrderSource = o.OrderSource.ToString(),
                    BranchName = isArabic && o.Branch.NameAr != null && o.Branch.NameAr != string.Empty
                        ? o.Branch.NameAr
                        : o.Branch.Name,
                    BranchAddress = o.Branch.Address,
                    CustomerName = o.Customer != null ? o.Customer.Name : null,
                    CustomerPhone = o.CustomerPhone ?? (o.Customer != null ? o.Customer.PhoneNumber : null),
                    DeliveryAddress = o.DeliveryAddress,
                    CreatedAtUtc = o.CreatedAt,
                    PaidAtUtc = o.PaidAt,
                    ScheduledForUtc = o.ScheduledFor,
                    PaymentStatus = o.Status == OrderStatus.PaymentCancelled
                        ? OrderPaymentHelper.FailedStatus
                        : OrderPaymentHelper.PendingStatus,
                    Subtotal = o.Subtotal,
                    DiscountAmount = o.DiscountAmount,
                    ServiceChargeAmount = o.ServiceChargeAmount,
                    TaxAmount = o.TaxAmount,
                    VoucherDiscountAmount = o.VoucherDiscountAmount,
                    DeliveryFee = o.DeliveryFee,
                    DeliveryZoneName = o.DeliveryZoneName,
                    TotalAmount = o.TotalAmount,
                    PaidAmount = o.Payments
                        .Where(p => p.DeletedAt == null)
                        .Select(p => (decimal?)p.Amount)
                        .Sum() ?? 0m,
                    PaymentMethod = o.Payments
                        .Where(p => p.DeletedAt == null)
                        .OrderByDescending(p => p.CreatedAt)
                        .Select(p => isArabic
                            ? p.PaymentMethodNameAr ?? p.PaymentMethodName ?? p.Method
                            : p.PaymentMethodName ?? p.Method)
                        .FirstOrDefault() ?? o.PaymentMethod,
                    IsRefunded = _context.RefundLogs.Any(refund => refund.OrderId == o.Id),
                    Items = o.OrderItems
                        .Where(i => i.DeletedAt == null)
                        .OrderBy(i => i.CreatedAt)
                        .Select(i => new PublicReceiptItemDto
                        {
                            Name = isArabic && i.Product.NameAr != null && i.Product.NameAr != string.Empty
                                ? i.Product.NameAr
                                : i.ProductName,
                            Quantity = i.Quantity,
                            UnitPrice = i.IsComplimentary ? 0m : i.Price,
                            LineTotalAmount = i.IsComplimentary
                                ? 0m
                                : i.LineTotalSnapshot ?? (i.Price + (i.Modifiers
                                      .Where(m => m.DeletedAt == null)
                                      .Select(m => (decimal?)(m.Price * m.Quantity))
                                      .Sum() ?? 0m)) * i.Quantity,
                            IsComplimentary = i.IsComplimentary,
                            Modifiers = i.Modifiers
                                .Where(m => m.DeletedAt == null)
                                .Select(m => new PublicReceiptModifierDto
                                {
                                    Name = isArabic && m.ModifierNameAr != null && m.ModifierNameAr != string.Empty
                                        ? m.ModifierNameAr
                                        : m.ModifierName,
                                    Quantity = m.Quantity,
                                    Price = m.Price
                                })
                                .ToList()
                        })
                        .ToList()
                });
        }

        private Task<SystemSettings?> GetReceiptSettingsAsync(Guid tenantId, CancellationToken ct)
        {
            return _context.SystemSettings
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId || s.TenantId == null)
                .OrderByDescending(s => s.TenantId == tenantId)
                .FirstOrDefaultAsync(ct);
        }

        private Task<string?> GetTenantLogoUrlAsync(Guid tenantId, CancellationToken ct)
        {
            return _context.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => t.LogoUrl)
                .FirstOrDefaultAsync(ct);
        }

        private static PublicReceiptRestaurantDto MapRestaurant(SystemSettings? settings, string? logoUrl)
        {
            return new PublicReceiptRestaurantDto
            {
                Name = string.IsNullOrWhiteSpace(settings?.RestaurantName)
                    ? DefaultRestaurantName
                    : settings.RestaurantName,
                LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl,
                Address = settings?.RestaurantAddress,
                Phone = settings?.RestaurantPhone,
                TaxNumber = settings?.TaxNumber,
                Currency = string.IsNullOrWhiteSpace(settings?.Currency) ? DefaultCurrency : settings.Currency,
                FooterNote = settings?.ReceiptFooterNote
            };
        }
    }
}
