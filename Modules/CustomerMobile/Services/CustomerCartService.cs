using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;

namespace RestaurantPos.Api.Modules.CustomerMobile.Services
{
    public class CustomerCartService : ICustomerCartService
    {
        private const int MaxQuantity = 99;

        private readonly PosDbContext _context;
        private readonly ICustomerMobileContext _mobileContext;

        public CustomerCartService(PosDbContext context, ICustomerMobileContext mobileContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mobileContext = mobileContext ?? throw new ArgumentNullException(nameof(mobileContext));
        }

        public async Task<CustomerCartDto> GetCartAsync(CancellationToken ct)
            => MapCart(await GetOrCreateCartAsync(ct));

        public async Task<CustomerCartDto> AddItemAsync(CustomerCartItemRequest request, CancellationToken ct)
        {
            ValidateQuantity(request.Quantity);
            var cart = await GetOrCreateCartAsync(ct);
            var product = await LoadProductAsync(request.ProductId, ct);
            ValidateOption(product, request.SelectedOptionId);
            ValidateModifiers(product, request.Modifiers);

            var itemId = Guid.NewGuid();
            var item = new CustomerCartItem
            {
                Id = itemId,
                TenantId = cart.TenantId,
                CartId = cart.Id,
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                SelectedOptionId = request.SelectedOptionId,
                Notes = NormalizeNullable(request.Notes),
                Modifiers = BuildModifiers(cart.TenantId, itemId, request.Modifiers)
            };

            _context.CustomerCartItems.Add(item);
            await _context.SaveChangesAsync(ct);
            return MapCart(await LoadCartAsync(cart.Id, ct));
        }

        public async Task<CustomerCartDto> UpdateItemAsync(Guid itemId, CustomerCartItemUpdateRequest request, CancellationToken ct)
        {
            ValidateQuantity(request.Quantity);
            var cart = await GetOrCreateCartAsync(ct);
            var item = cart.Items.FirstOrDefault(i => i.Id == itemId)
                ?? throw new NotFoundException("Cart item was not found.");

            var product = await LoadProductAsync(item.ProductId, ct);
            ValidateModifiers(product, request.Modifiers);

            item.Quantity = request.Quantity;
            item.Notes = NormalizeNullable(request.Notes);
            _context.CustomerCartItemModifiers.RemoveRange(item.Modifiers);
            item.Modifiers = BuildModifiers(cart.TenantId, item.Id, request.Modifiers);
            _context.CustomerCartItemModifiers.AddRange(item.Modifiers);

            await _context.SaveChangesAsync(ct);
            return MapCart(await LoadCartAsync(cart.Id, ct));
        }

        public async Task<CustomerCartDto> RemoveItemAsync(Guid itemId, CancellationToken ct)
        {
            var cart = await GetOrCreateCartAsync(ct);
            var item = cart.Items.FirstOrDefault(i => i.Id == itemId)
                ?? throw new NotFoundException("Cart item was not found.");

            _context.CustomerCartItems.Remove(item);
            await _context.SaveChangesAsync(ct);
            return MapCart(await LoadCartAsync(cart.Id, ct));
        }

        public async Task ClearAsync(CancellationToken ct)
        {
            var cart = await GetOrCreateCartAsync(ct);
            _context.CustomerCartItems.RemoveRange(cart.Items);
            await _context.SaveChangesAsync(ct);
        }

        private async Task<CustomerCart> GetOrCreateCartAsync(CancellationToken ct)
        {
            var customerId = _mobileContext.GetCustomerId();
            var cart = await _context.CustomerCarts
                .Include(c => c.Items)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p.Options)
                .Include(c => c.Items)
                    .ThenInclude(i => i.Modifiers)
                        .ThenInclude(m => m.Modifier)
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
            await _context.SaveChangesAsync(ct);
            return await LoadCartAsync(cart.Id, ct);
        }

        private async Task<CustomerCart> LoadCartAsync(Guid cartId, CancellationToken ct)
        {
            return await _context.CustomerCarts
                .Include(c => c.Items)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p.Options)
                .Include(c => c.Items)
                    .ThenInclude(i => i.Modifiers)
                        .ThenInclude(m => m.Modifier)
                .FirstAsync(c => c.Id == cartId, ct);
        }

        private async Task<Product> LoadProductAsync(Guid productId, CancellationToken ct)
        {
            return await _context.Products
                .Include(p => p.Options)
                .Include(p => p.ProductModifierGroups)
                    .ThenInclude(pmg => pmg.ModifierGroup)
                        .ThenInclude(g => g.Modifiers)
                .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive, ct)
                ?? throw new ValidationException("Product is invalid or inactive.");
        }

        private static List<CustomerCartItemModifier> BuildModifiers(
            Guid tenantId,
            Guid cartItemId,
            IEnumerable<CustomerCartItemModifierRequest> modifiers)
        {
            return modifiers.Select(m => new CustomerCartItemModifier
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CartItemId = cartItemId,
                ModifierId = m.ModifierId,
                Quantity = Math.Clamp(m.Quantity, 1, MaxQuantity)
            }).ToList();
        }

        private static CustomerCartDto MapCart(CustomerCart cart)
        {
            var items = cart.Items.OrderBy(i => i.CreatedAt).Select(MapCartItem).ToList();
            return new CustomerCartDto
            {
                Id = cart.Id,
                Items = items,
                Subtotal = items.Sum(i => i.LineTotal)
            };
        }

        private static CustomerCartItemDto MapCartItem(CustomerCartItem item)
        {
            var unitPrice = ResolveUnitPrice(item);
            var selectedOption = item.SelectedOptionId.HasValue
                ? item.Product.Options.FirstOrDefault(o => o.Id == item.SelectedOptionId.Value)
                : null;
            var modifiers = item.Modifiers.Select(m => new CustomerCartItemModifierDto
            {
                ModifierId = m.ModifierId,
                ModifierName = m.Modifier.Name,
                ModifierNameAr = m.Modifier.NameAr,
                Price = m.Modifier.PriceAdjustment,
                Quantity = m.Quantity
            }).ToList();

            return new CustomerCartItemDto
            {
                Id = item.Id,
                ProductId = item.ProductId,
                ProductName = item.Product.Name,
                ProductNameAr = item.Product.NameAr,
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                LineTotal = (unitPrice + modifiers.Sum(m => m.Price * m.Quantity)) * item.Quantity,
                SelectedOptionId = item.SelectedOptionId,
                SelectedOptionName = selectedOption?.Name,
                SelectedOptionNameAr = selectedOption?.NameAr,
                Notes = item.Notes,
                Modifiers = modifiers
            };
        }

        private static decimal ResolveUnitPrice(CustomerCartItem item)
        {
            if (item.SelectedOptionId.HasValue)
            {
                var option = item.Product.Options.FirstOrDefault(o => o.Id == item.SelectedOptionId.Value);
                if (option != null)
                    return option.Price;
            }

            return item.Product.DiscountedPrice ?? item.Product.BasePrice;
        }

        private static void ValidateOption(Product product, Guid? selectedOptionId)
        {
            if (!selectedOptionId.HasValue)
                return;

            var valid = product.Options.Any(o => o.Id == selectedOptionId.Value && o.IsActive);
            if (!valid)
                throw new ValidationException("Selected product option is invalid.");
        }

        private static void ValidateModifiers(Product product, IEnumerable<CustomerCartItemModifierRequest> modifiers)
        {
            var validModifierIds = product.ProductModifierGroups
                .SelectMany(pmg => pmg.ModifierGroup.Modifiers)
                .Select(m => m.Id)
                .ToHashSet();

            if (modifiers.Any(m => !validModifierIds.Contains(m.ModifierId)))
                throw new ValidationException("One or more modifiers are invalid for this product.");
        }

        private static void ValidateQuantity(int quantity)
        {
            if (quantity is < 1 or > MaxQuantity)
                throw new ValidationException($"Quantity must be between 1 and {MaxQuantity}.");
        }

        private static string? NormalizeNullable(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
