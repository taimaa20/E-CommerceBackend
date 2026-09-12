using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories;

public sealed class OnlineShoppingRepository : IOnlineShoppingRepository
{
    private readonly PosDbContext _context;

    public OnlineShoppingRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<Branch>> GetActiveBranchesAsync(Guid tenantId, CancellationToken ct)
        => await _context.Branches
            .AsNoTracking()
            .Where(branch => branch.TenantId == tenantId && branch.IsActive)
            .OrderByDescending(branch => branch.IsMainBranch)
            .ThenBy(branch => branch.Name)
            .ToListAsync(ct);

    public async Task<Branch> ResolveActiveBranchAsync(Guid tenantId, Guid? branchId, CancellationToken ct)
        => await _context.Branches
            .AsNoTracking()
            .Where(branch => branch.TenantId == tenantId && branch.IsActive)
            .Where(branch => branchId.HasValue ? branch.Id == branchId : branch.IsMainBranch)
            .FirstOrDefaultAsync(ct)
            ?? throw new ValidationException("The selected branch is invalid or inactive.");

    public async Task<IReadOnlyList<Product>> GetBranchProductsAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken ct)
    {
        var configuredProductIds = _context.BranchProducts
            .AsNoTracking()
            .Where(config => config.BranchId == branchId);
        var hasProductConfiguration = await configuredProductIds.AnyAsync(ct);
        var availableProductIds = configuredProductIds
            .Where(config => config.IsAvailable && config.IsVisible)
            .Select(config => config.ProductId);

        var query = _context.Products
            .AsNoTracking()
            .AsSplitQuery()
            .Include(product => product.Category)
            .Include(product => product.RecipeItems)
            .Include(product => product.Options)
                .ThenInclude(option => option.RecipeItems)
            .Include(product => product.ProductModifierGroups)
                .ThenInclude(link => link.ModifierGroup)
                    .ThenInclude(group => group.Modifiers)
                        .ThenInclude(modifier => modifier.RecipeItems)
            .Where(product => product.TenantId == tenantId && product.IsActive && !product.IsSoon);

        if (hasProductConfiguration)
            query = query.Where(product => availableProductIds.Contains(product.Id));

        return await query.OrderBy(product => product.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetBranchStockAsync(Guid branchId, CancellationToken ct)
        => await _context.StockBatches
            .AsNoTracking()
            .Where(batch =>
                batch.BranchId == branchId &&
                batch.RemainingQuantity > 0 &&
                batch.Status == BatchStatus.Good &&
                (batch.PurchaseOrderId == null || batch.PurchaseOrder!.Status == PurchaseOrderStatus.Approved))
            .GroupBy(batch => batch.MaterialId)
            .Select(group => new { MaterialId = group.Key, Quantity = group.Sum(batch => batch.RemainingQuantity) })
            .ToDictionaryAsync(row => row.MaterialId, row => row.Quantity, ct);

    public async Task<IReadOnlyList<DeliveryZone>> GetDeliveryZonesAsync(Guid branchId, CancellationToken ct)
        => await _context.DeliveryZones
            .AsNoTracking()
            .Where(zone => zone.BranchId == branchId && zone.IsActive)
            .OrderBy(zone => zone.DisplayOrder)
            .ThenBy(zone => zone.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PaymentMethod>> GetPaymentMethodsAsync(Guid branchId, CancellationToken ct)
    {
        var configuredIds = _context.BranchPaymentMethods
            .AsNoTracking()
            .Where(config => config.BranchId == branchId);
        var hasConfiguration = await configuredIds.AnyAsync(ct);
        var enabledIds = configuredIds.Where(config => config.IsEnabled).Select(config => config.PaymentMethodId);
        var query = _context.PaymentMethods.AsNoTracking().Where(method => method.IsActive);
        if (hasConfiguration)
            query = query.Where(method => enabledIds.Contains(method.Id));

        return await query.OrderBy(method => method.DisplayOrder).ThenBy(method => method.NameEn).ToListAsync(ct);
    }

    public Task<Customer?> GetCustomerByPhoneAsync(string phone, CancellationToken ct)
        => _context.Customers.FirstOrDefaultAsync(customer => customer.PhoneNumber == phone, ct);

    public async Task AddCustomerAsync(Customer customer, CancellationToken ct)
    {
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(ct);
    }

    private const int OrderLookupCandidateLimit = 10;

    public async Task<IReadOnlyList<OnlineOrderLookupRow>> FindOnlineOrdersByNumberAsync(
        string orderNumber,
        CancellationToken ct)
        => await _context.Orders
            .AsNoTracking()
            .Where(order =>
                order.OrderSource == OrderSource.Online &&
                order.ClientOrderUuid != null &&
                (order.DisplayOrderNumber == orderNumber ||
                 order.PublicOrderNumber == orderNumber ||
                 order.OrderNumber == orderNumber))
            .OrderByDescending(order => order.CreatedAt)
            .Take(OrderLookupCandidateLimit)
            .Select(order => new OnlineOrderLookupRow
            {
                OrderId = order.Id,
                ClientOrderUuid = order.ClientOrderUuid!,
                CustomerPhone = order.CustomerPhone ?? (order.Customer != null ? order.Customer.PhoneNumber : null),
                CustomerEmail = order.CustomerEmail ?? (order.Customer != null ? order.Customer.Email : null)
            })
            .ToListAsync(ct);

    public Task<OnlineShoppingOrderRow?> GetOnlineOrderAsync(
        Guid orderId,
        string clientOrderUuid,
        CancellationToken ct)
        => _context.Orders
            .AsNoTracking()
            .Where(order =>
                order.Id == orderId &&
                order.OrderSource == OrderSource.Online &&
                order.ClientOrderUuid == clientOrderUuid)
            .Select(order => new OnlineShoppingOrderRow
            {
                OrderId = order.Id,
                OrderNumber = order.DisplayOrderNumber ?? order.PublicOrderNumber ?? order.OrderNumber,
                TenantId = order.TenantId,
                BranchId = order.BranchId,
                OrderType = order.OrderType.ToString(),
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                TotalAmount = order.TotalAmount,
                PaidAmount = order.Payments
                    .Where(payment => payment.DeletedAt == null)
                    .Select(payment => (decimal?)payment.Amount)
                    .Sum() ?? 0m,
                PaymentMethod = order.PaymentMethod,
                SubmittedPaymentReference = order.SubmittedPaymentReference,
                PaymentMethodName = order.Payments
                    .Where(payment => payment.DeletedAt == null)
                    .OrderByDescending(payment => payment.CreatedAt)
                    .Select(payment => payment.PaymentMethodName ?? payment.Method)
                    .FirstOrDefault(),
                PaymentMethodNameAr = order.Payments
                    .Where(payment => payment.DeletedAt == null)
                    .OrderByDescending(payment => payment.CreatedAt)
                    .Select(payment => payment.PaymentMethodNameAr)
                    .FirstOrDefault(),
                LastPaymentId = order.Payments
                    .Where(payment => payment.DeletedAt == null)
                    .OrderByDescending(payment => payment.CreatedAt)
                    .Select(payment => (Guid?)payment.Id)
                    .FirstOrDefault(),
                LastPaymentClientActionId = order.Payments
                    .Where(payment => payment.DeletedAt == null)
                    .OrderByDescending(payment => payment.CreatedAt)
                    .Select(payment => payment.ClientActionId)
                    .FirstOrDefault(),
                PaidAt = order.PaidAt,
                ScheduledFor = order.ScheduledFor,
                DispatchedAt = order.DispatchedAt,
                AllItemsReady = order.OrderItems.Any() && !order.OrderItems.Any(item => !item.IsReady),
                IsRefunded = _context.RefundLogs.Any(refund => refund.OrderId == order.Id),
                CustomerName = order.CustomerName ?? (order.Customer != null ? order.Customer.Name : string.Empty),
                CustomerEmail = order.CustomerEmail ?? (order.Customer != null ? order.Customer.Email ?? string.Empty : string.Empty),
                CustomerPhone = order.CustomerPhone ?? (order.Customer != null ? order.Customer.PhoneNumber : string.Empty),
                DeliveryAddress = order.DeliveryAddress,
                DeliveryNotes = order.DeliveryNotes,
                DeliveryZoneName = order.DeliveryZoneName,
                DeliveryFee = order.DeliveryFee,
                Branch = new OnlineShoppingBranchDto
                {
                    Id = order.Branch.Id,
                    Name = order.Branch.Name,
                    NameAr = order.Branch.NameAr,
                    Code = order.Branch.Code,
                    Address = order.Branch.Address,
                    Phone = order.Branch.Phone,
                    IsMainBranch = order.Branch.IsMainBranch
                },
                Items = order.OrderItems
                    .OrderBy(item => item.CreatedAt)
                    .Select(item => new OnlineShoppingOrderItemDto
                    {
                        Name = item.ProductName,
                        NameAr = item.Product.NameAr,
                        Quantity = item.Quantity,
                        UnitPrice = item.IsComplimentary ? 0m : item.Price,
                        LineTotal = item.IsComplimentary
                            ? 0m
                            : item.LineTotalSnapshot.HasValue
                                ? item.LineTotalSnapshot.Value
                                : (item.Price + item.Modifiers
                                    .Select(modifier => (decimal?)(modifier.Price * modifier.Quantity))
                                    .Sum() ?? 0m) * item.Quantity
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);
}
