using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.CustomerMobile.Services
{
    public class CustomerCheckoutService : ICustomerCheckoutService
    {
        private const int MaxQuantity = 99;

        private readonly PosDbContext _context;
        private readonly ICustomerMobileContext _mobileContext;
        private readonly ICustomerMobileBranchService _branchService;

        public CustomerCheckoutService(
            PosDbContext context,
            ICustomerMobileContext mobileContext,
            ICustomerMobileBranchService branchService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mobileContext = mobileContext ?? throw new ArgumentNullException(nameof(mobileContext));
            _branchService = branchService ?? throw new ArgumentNullException(nameof(branchService));
        }

        public async Task<CustomerOrderCalculationResponse> CalculateAsync(
            CustomerOrderCalculateRequest request,
            CancellationToken ct)
        {
            var orderType = ParseOrderType(request.OrderType);
            ValidateRequest(request, orderType);

            var deliveryFee = await ResolveDeliveryFeeAsync(orderType, request.DeliveryAddressId, ct);
            var products = await LoadProductsAsync(request.Items, ct);
            var items = request.Items.Select(i => CalculateItem(i, products[i.ProductId])).ToList();
            var subtotal = items.Sum(i => i.LineTotal);

            return new CustomerOrderCalculationResponse
            {
                Subtotal = subtotal,
                Discounts = 0m,
                Tax = 0m,
                DeliveryFee = deliveryFee,
                ServiceFee = 0m,
                FinalTotal = subtotal + deliveryFee,
                Items = items
            };
        }

        public Task<List<PaymentMethodDto>> GetPaymentMethodsAsync(Guid branchId, CancellationToken ct)
            => _branchService.GetPaymentMethodsAsync(branchId, ct);

        private async Task<decimal> ResolveDeliveryFeeAsync(
            CustomerMobileOrderType orderType,
            Guid? deliveryAddressId,
            CancellationToken ct)
        {
            if (orderType == CustomerMobileOrderType.Pickup)
                return 0m;

            var customerId = _mobileContext.GetCustomerId();
            var address = await _context.CustomerAddresses
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.CustomerId == customerId && a.Id == deliveryAddressId, ct)
                ?? throw new ValidationException("Delivery address is invalid.");

            if (!address.DeliveryZoneId.HasValue)
                throw new ValidationException("Delivery address must have a delivery zone.");

            // Mobile orders always land on the Main Branch, so only Main-Branch zones apply.
            var zone = await _context.DeliveryZones
                .AsNoTracking()
                .FirstOrDefaultAsync(z => z.Id == address.DeliveryZoneId.Value && z.IsActive && z.Branch!.IsMainBranch, ct)
                ?? throw new ValidationException("Delivery zone is invalid or inactive.");

            return zone.DeliveryFee;
        }

        private async Task<Dictionary<Guid, Product>> LoadProductsAsync(
            List<CustomerOrderItemRequest> items,
            CancellationToken ct)
        {
            var productIds = items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _context.Products
                .AsNoTracking()
                .Include(p => p.Options)
                .Include(p => p.ProductModifierGroups)
                    .ThenInclude(pmg => pmg.ModifierGroup)
                        .ThenInclude(g => g.Modifiers)
                .Where(p => productIds.Contains(p.Id) && p.IsActive)
                .ToDictionaryAsync(p => p.Id, ct);

            if (products.Count != productIds.Count)
                throw new ValidationException("One or more products are invalid or inactive.");

            return products;
        }

        private static CustomerOrderCalculationItemDto CalculateItem(
            CustomerOrderItemRequest item,
            Product product)
        {
            ValidateQuantity(item.Quantity);
            var option = ResolveOption(product, item.SelectedOptionId);
            var modifierTotal = ResolveModifierTotal(product, item.Modifiers);
            var unitPrice = option?.Price ?? product.DiscountedPrice ?? product.BasePrice;

            return new CustomerOrderCalculationItemDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                ModifiersTotal = modifierTotal,
                LineTotal = (unitPrice + modifierTotal) * item.Quantity
            };
        }

        private static ProductOption? ResolveOption(Product product, Guid? selectedOptionId)
        {
            if (!selectedOptionId.HasValue)
                return null;

            return product.Options.FirstOrDefault(o => o.Id == selectedOptionId.Value && o.IsActive)
                ?? throw new ValidationException("Selected product option is invalid.");
        }

        private static decimal ResolveModifierTotal(
            Product product,
            IEnumerable<CustomerCartItemModifierRequest> selected)
        {
            var lookup = product.ProductModifierGroups
                .SelectMany(pmg => pmg.ModifierGroup.Modifiers)
                .Where(m => m.IsActive)
                .ToDictionary(m => m.Id, m => m.PriceAdjustment);

            var total = 0m;
            foreach (var item in selected)
            {
                if (!lookup.TryGetValue(item.ModifierId, out var price))
                    throw new ValidationException("One or more modifiers are invalid for this product.");

                total += price * Math.Clamp(item.Quantity, 1, MaxQuantity);
            }

            return total;
        }

        private static void ValidateRequest(
            CustomerOrderCalculateRequest request,
            CustomerMobileOrderType orderType)
        {
            if (request.Items.Count == 0)
                throw new ValidationException("At least one item is required.");

            if (orderType == CustomerMobileOrderType.Delivery && !request.DeliveryAddressId.HasValue)
                throw new ValidationException("Delivery address is required.");
        }

        private static void ValidateQuantity(int quantity)
        {
            if (quantity is < 1 or > MaxQuantity)
                throw new ValidationException($"Quantity must be between 1 and {MaxQuantity}.");
        }

        private static CustomerMobileOrderType ParseOrderType(string value)
            => Enum.TryParse<CustomerMobileOrderType>(value, true, out var parsed)
                ? parsed
                : throw new ValidationException("Order type is invalid.");
    }
}
