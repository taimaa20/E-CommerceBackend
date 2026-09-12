using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;

namespace RestaurantPos.Api.Modules.CustomerMobile.Services
{
    public class CustomerReorderService : ICustomerReorderService
    {
        private const int MaxQuantity = 99;

        private readonly PosDbContext _context;
        private readonly ICustomerMobileContext _mobileContext;
        private readonly ICustomerCartService _cartService;

        public CustomerReorderService(
            PosDbContext context,
            ICustomerMobileContext mobileContext,
            ICustomerCartService cartService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mobileContext = mobileContext ?? throw new ArgumentNullException(nameof(mobileContext));
            _cartService = cartService ?? throw new ArgumentNullException(nameof(cartService));
        }

        public async Task<CustomerCartDto> ReorderAsync(Guid orderId, CancellationToken ct)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            var order = await LoadOwnedOrderAsync(orderId, ct);
            var products = await LoadProductsAsync(order.OrderItems, ct);
            var cart = await GetOrCreateCartAsync(ct);

            _context.CustomerCartItems.RemoveRange(cart.Items);
            foreach (var item in order.OrderItems)
            {
                cart.Items.Add(BuildCartItem(item, products[item.ProductId], cart.TenantId, cart.Id));
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return await _cartService.GetCartAsync(ct);
        }

        private async Task<Order> LoadOwnedOrderAsync(Guid orderId, CancellationToken ct)
        {
            var customerId = _mobileContext.GetCustomerId();
            var link = await _context.CustomerMobileOrders
                .AsNoTracking()
                .Include(m => m.Order)
                    .ThenInclude(o => o.OrderItems)
                        .ThenInclude(i => i.Modifiers)
                .FirstOrDefaultAsync(m => m.CustomerId == customerId && m.OrderId == orderId, ct)
                ?? throw new NotFoundException("Customer order was not found.");

            return link.Order;
        }

        private async Task<Dictionary<Guid, Product>> LoadProductsAsync(
            IEnumerable<OrderItem> orderItems,
            CancellationToken ct)
        {
            var productIds = orderItems.Select(i => i.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Include(p => p.Options)
                .Include(p => p.ProductModifierGroups)
                    .ThenInclude(pmg => pmg.ModifierGroup)
                        .ThenInclude(g => g.Modifiers)
                .Where(p => productIds.Contains(p.Id) && p.IsActive)
                .ToDictionaryAsync(p => p.Id, ct);

            if (products.Count != productIds.Count)
                throw new ValidationException("One or more products are no longer available.");

            return products;
        }

        private async Task<CustomerCart> GetOrCreateCartAsync(CancellationToken ct)
        {
            var customerId = _mobileContext.GetCustomerId();
            var cart = await _context.CustomerCarts
                .Include(c => c.Items)
                    .ThenInclude(i => i.Modifiers)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.IsActive, ct);

            if (cart != null)
                return cart;

            cart = new CustomerCart
            {
                Id = Guid.NewGuid(),
                TenantId = _mobileContext.GetTenantId(),
                CustomerId = customerId
            };
            _context.CustomerCarts.Add(cart);
            return cart;
        }

        private static CustomerCartItem BuildCartItem(
            OrderItem orderItem,
            Product product,
            Guid tenantId,
            Guid cartId)
        {
            ValidateQuantity(orderItem.Quantity);
            ValidateOption(product, orderItem.SelectedOptionId);
            var modifiers = BuildModifiers(product, orderItem.Modifiers, tenantId);

            return new CustomerCartItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CartId = cartId,
                ProductId = orderItem.ProductId,
                Quantity = orderItem.Quantity,
                SelectedOptionId = orderItem.SelectedOptionId,
                Notes = orderItem.Notes,
                Modifiers = modifiers
            };
        }

        private static List<CustomerCartItemModifier> BuildModifiers(
            Product product,
            IEnumerable<OrderItemModifier> orderModifiers,
            Guid tenantId)
        {
            var validModifierIds = product.ProductModifierGroups
                .SelectMany(pmg => pmg.ModifierGroup.Modifiers)
                .Where(m => m.IsActive)
                .Select(m => m.Id)
                .ToHashSet();

            return orderModifiers
                .Where(m => m.ModifierId.HasValue)
                .Select(m =>
                {
                    if (!validModifierIds.Contains(m.ModifierId!.Value))
                        throw new ValidationException("One or more modifiers are no longer available.");

                    return new CustomerCartItemModifier
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ModifierId = m.ModifierId.Value,
                        Quantity = Math.Clamp(m.Quantity, 1, MaxQuantity)
                    };
                })
                .ToList();
        }

        private static void ValidateOption(Product product, Guid? selectedOptionId)
        {
            if (!selectedOptionId.HasValue)
                return;

            if (!product.Options.Any(o => o.Id == selectedOptionId.Value && o.IsActive))
                throw new ValidationException("Selected product option is no longer available.");
        }

        private static void ValidateQuantity(int quantity)
        {
            if (quantity is < 1 or > MaxQuantity)
                throw new ValidationException($"Quantity must be between 1 and {MaxQuantity}.");
        }
    }
}
