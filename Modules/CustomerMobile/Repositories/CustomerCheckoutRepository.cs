using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.CustomerMobile.Repositories;

public interface ICustomerCheckoutRepository
{
    Task<CustomerCheckoutOrder?> GetOwnedOrderAsync(
        Guid tenantId,
        Guid customerId,
        Guid orderId,
        CancellationToken ct);
}

public sealed class CustomerCheckoutRepository : ICustomerCheckoutRepository
{
    private readonly PosDbContext _context;

    public CustomerCheckoutRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<CustomerCheckoutOrder?> GetOwnedOrderAsync(
        Guid tenantId,
        Guid customerId,
        Guid orderId,
        CancellationToken ct)
        => _context.CustomerMobileOrders
            .AsNoTracking()
            .Where(mobileOrder =>
                mobileOrder.TenantId == tenantId &&
                mobileOrder.CustomerId == customerId &&
                mobileOrder.OrderId == orderId)
            .Select(mobileOrder => new CustomerCheckoutOrder
            {
                OrderId = mobileOrder.OrderId,
                OrderNumber = mobileOrder.Order.OrderNumber,
                BranchId = mobileOrder.Order.BranchId,
                Status = mobileOrder.Order.Status,
                PaidAt = mobileOrder.Order.PaidAt,
                TotalAmount = mobileOrder.Order.TotalAmount,
                PaymentMethod = mobileOrder.Order.PaymentMethod,
                HasRecordedPayment = mobileOrder.Order.Payments.Any(),
                FirstName = mobileOrder.Customer.FirstName,
                LastName = mobileOrder.Customer.LastName,
                Email = mobileOrder.Customer.Email,
                PhoneNumber = mobileOrder.Customer.PhoneNumber ?? mobileOrder.Customer.MobileNumber,
                DeliveryAddress = mobileOrder.Order.DeliveryAddress
            })
            .FirstOrDefaultAsync(ct);
}

public sealed class CustomerCheckoutOrder
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public Guid BranchId { get; init; }
    public OrderStatus Status { get; init; }
    public DateTime? PaidAt { get; init; }
    public decimal TotalAmount { get; init; }
    public string? PaymentMethod { get; init; }
    public bool HasRecordedPayment { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string? DeliveryAddress { get; init; }
}
