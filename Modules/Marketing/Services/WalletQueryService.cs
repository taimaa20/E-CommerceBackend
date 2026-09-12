using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.DTOs;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Marketing.Services
{
    /// <inheritdoc cref="IWalletQueryService"/>
    public class WalletQueryService : IWalletQueryService
    {
        private const int MaxPageSize = 100;
        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenant;

        public WalletQueryService(PosDbContext context, ITenantResolver tenant)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenant = tenant ?? throw new ArgumentNullException(nameof(tenant));
        }

        // These are tenant-FACING reads: every lookup is explicitly scoped to the resolved tenant
        // (header for admin/cashier, tenant_id claim for mobile) so a caller can never read another
        // tenant's wallet by GUID. IgnoreQueryFilters is retained only to add the explicit predicate
        // ourselves (the tenant_id claim path may not satisfy the global filter), never to bypass it.

        public async Task<WalletDto?> GetWalletAsync(Guid customerId, CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();
            var w = await _context.CustomerWallets.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(x => x.CustomerId == customerId && x.TenantId == tenantId, ct);
            return w is null ? null : MapWallet(w);
        }

        public async Task EnsureCustomerInCurrentTenantAsync(Guid customerId, CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();
            var exists = await _context.Customers.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(c => c.Id == customerId && c.TenantId == tenantId, ct);
            if (!exists) throw new NotFoundException("Customer was not found.");
        }

        public async Task<LoyaltyProfileDto?> GetProfileAsync(Guid customerId, bool isArabic, CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();
            var customer = await _context.Customers.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId, ct);
            if (customer is null) return null;

            var wallet = await _context.CustomerWallets.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(w => w.CustomerId == customerId && w.TenantId == tenantId, ct);

            return new LoyaltyProfileDto
            {
                CustomerId = customer.Id,
                LoyaltyCustomerCode = customer.LoyaltyCustomerCode,
                Name = (isArabic ? customer.NameAr : customer.Name) ?? customer.Name,
                PhoneNumber = customer.PhoneNumber,
                Tier = customer.Tier.ToString(),
                AvailablePoints = wallet?.AvailablePoints ?? customer.LoyaltyPoints,
                LifetimePoints = wallet?.LifetimePoints ?? 0m,
                Status = (wallet?.Status ?? WalletStatus.Active).ToString()
            };
        }

        public async Task<PaginatedResponse<WalletTransactionDto>> GetHistoryAsync(
            Guid customerId, int page, int pageSize, CancellationToken ct)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > MaxPageSize ? 20 : pageSize;
            var tenantId = _tenant.GetTenantId();

            var query = _context.WalletTransactions.IgnoreQueryFilters().AsNoTracking()
                .Where(t => t.CustomerId == customerId && t.TenantId == tenantId)
                .OrderByDescending(t => t.CreatedAt);

            var total = await query.CountAsync(ct);
            var items = await query
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(t => new WalletTransactionDto
                {
                    Id = t.Id,
                    Type = t.Type.ToString(),
                    Points = t.Points,
                    BalanceAfter = t.BalanceAfter,
                    Status = t.Status.ToString(),
                    Source = t.Source.ToString(),
                    OrderId = t.OrderId,
                    Reason = t.Reason,
                    CreatedAt = t.CreatedAt,
                    ExpiresAt = t.ExpiresAt
                })
                .ToListAsync(ct);

            return new PaginatedResponse<WalletTransactionDto>
            {
                Items = items,
                TotalCount = total,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        public async Task<PaginatedResponse<WalletSearchItemDto>> SearchAsync(
            string? term, int page, int pageSize, bool isArabic, CancellationToken ct)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > MaxPageSize ? 20 : pageSize;

            var tenantId = _tenant.GetTenantId();

            // Tenant-scoped via the explicit filter (admin grid must never cross tenants).
            var query = _context.Customers.AsNoTracking().Where(c => c.TenantId == tenantId);

            term = term?.Trim();
            if (!string.IsNullOrEmpty(term))
            {
                var like = $"%{term}%";
                query = query.Where(c =>
                    EF.Functions.ILike(c.Name, like)
                    || (c.NameAr != null && EF.Functions.ILike(c.NameAr, like))
                    || (c.PhoneNumber != null && EF.Functions.ILike(c.PhoneNumber, like))
                    || (c.LoyaltyCustomerCode != null && EF.Functions.ILike(c.LoyaltyCustomerCode, like)));
            }

            var ordered = query.OrderByDescending(c => c.LastVisit);
            var total = await ordered.CountAsync(ct);

            // Project against the wallet aggregate (left join) so customers without a wallet still appear.
            var items = await ordered
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.NameAr,
                    c.PhoneNumber,
                    c.LoyaltyCustomerCode,
                    c.Tier,
                    c.LoyaltyPoints,
                    Wallet = _context.CustomerWallets.IgnoreQueryFilters()
                        .Where(w => w.CustomerId == c.Id)
                        .Select(w => new { w.AvailablePoints, w.Status })
                        .FirstOrDefault()
                })
                .ToListAsync(ct);

            return new PaginatedResponse<WalletSearchItemDto>
            {
                Items = items.Select(c => new WalletSearchItemDto
                {
                    CustomerId = c.Id,
                    Name = (isArabic ? c.NameAr : c.Name) ?? c.Name,
                    PhoneNumber = c.PhoneNumber,
                    LoyaltyCustomerCode = c.LoyaltyCustomerCode,
                    Tier = c.Tier.ToString(),
                    AvailablePoints = c.Wallet?.AvailablePoints ?? c.LoyaltyPoints,
                    Status = (c.Wallet?.Status ?? WalletStatus.Active).ToString(),
                    HasWallet = c.Wallet != null
                }).ToList(),
                TotalCount = total,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        private static WalletDto MapWallet(CustomerWallet w) => new()
        {
            CustomerId = w.CustomerId,
            AvailablePoints = w.AvailablePoints,
            PendingPoints = w.PendingPoints,
            ExpiredPoints = w.ExpiredPoints,
            RedeemedPoints = w.RedeemedPoints,
            LifetimePoints = w.LifetimePoints,
            LifetimeSpend = w.LifetimeSpend,
            CurrencyCode = w.CurrencyCode,
            TotalOrders = w.TotalOrders,
            TotalVisits = w.TotalVisits,
            Status = w.Status.ToString(),
            CurrentTierId = w.CurrentTierId,
            LastRecalculatedAt = w.LastRecalculatedAt
        };
    }
}
