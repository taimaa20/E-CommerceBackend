using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services;

public sealed partial class OnlineShoppingService : IOnlineShoppingService
{
    private readonly IOnlineShoppingRepository _repository;
    private readonly IProductStockLedger _productStockLedger;
    private readonly IStorefrontOfferService _offerService;
    // Long enough that a suffix match cannot be brute-forced from an order number
    // alone, short enough to accept a local number typed without its country code.
    private const int MinimumPhoneDigits = 7;
    private const string OrderLookupFailureMessage =
        "We could not find an order matching those details.";

    public OnlineShoppingService(
        IOnlineShoppingRepository repository,
        IProductStockLedger productStockLedger,
        IStorefrontOfferService offerService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _productStockLedger = productStockLedger ?? throw new ArgumentNullException(nameof(productStockLedger));
        _offerService = offerService ?? throw new ArgumentNullException(nameof(offerService));
    }

    public async Task<OnlineMenuOrderContextDto> GetOrderContextAsync(
        Guid tenantId,
        Guid? branchId,
        CancellationToken ct)
    {
        var branch = await _repository.ResolveActiveBranchAsync(tenantId, branchId, ct);
        var branches = await _repository.GetActiveBranchesAsync(tenantId, ct);
        var zones = await _repository.GetDeliveryZonesAsync(branch.Id, ct);
        var paymentMethods = await _repository.GetPaymentMethodsAsync(branch.Id, ct);
        return new OnlineMenuOrderContextDto
        {
            Branches = branches.Select(MapBranch).ToList(),
            ProductAvailability = await BuildProductAvailabilityAsync(tenantId, branch.Id, ct),
            DeliveryZones = zones.Select(zone => new OnlineShoppingDeliveryZoneDto
            {
                Id = zone.Id,
                Name = zone.Name,
                NameAr = zone.NameAr,
                DeliveryFee = zone.DeliveryFee
            }).ToList(),
            PaymentMethods = MapPaymentMethods(paymentMethods)
        };
    }

    public async Task<IReadOnlyList<OnlineShoppingProductAvailabilityDto>> GetProductAvailabilityAsync(
        Guid tenantId,
        Guid? branchId,
        CancellationToken ct)
    {
        var branch = await _repository.ResolveActiveBranchAsync(tenantId, branchId, ct);
        return await BuildProductAvailabilityAsync(tenantId, branch.Id, ct);
    }

    private async Task<IReadOnlyList<OnlineShoppingProductAvailabilityDto>> BuildProductAvailabilityAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken ct)
    {
        var sourceProducts = await _repository.GetBranchProductsAsync(tenantId, branchId, ct);
        var stock = await _repository.GetBranchStockAsync(branchId, ct);
        // Finished goods carry their own stock instead of a recipe, so their availability comes
        // from the product ledger rather than from raw-material levels.
        var finishedGoods = await _productStockLedger.GetOnHandAsync(
            branchId,
            sourceProducts.Select(product => product.Id).ToList(),
            ct);
        return sourceProducts.Select(product => MapAvailability(product, stock, finishedGoods)).ToList();
    }

    public async Task<Customer> ResolveCustomerAsync(
        Guid tenantId,
        string name,
        string phone,
        string? email,
        CancellationToken ct)
    {
        var normalizedName = RequireText(name, "Customer name");
        var normalizedPhone = RequireText(phone, "Customer phone");
        var customer = await _repository.GetCustomerByPhoneAsync(normalizedPhone, ct);
        if (customer != null)
            return customer;

        customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = normalizedName,
            PhoneNumber = normalizedPhone,
            Email = Normalize(email),
            Tier = CustomerTier.Standard,
            LastVisit = DateTime.UtcNow
        };
        await _repository.AddCustomerAsync(customer, ct);
        return customer;
    }

    public async Task<string?> ValidateAvailabilityAsync(
        Guid tenantId,
        Guid branchId,
        IReadOnlyList<OrderItemCreateDto> items,
        CancellationToken ct)
    {
        var products = await _repository.GetBranchProductsAsync(tenantId, branchId, ct);
        var productLookup = products.ToDictionary(product => product.Id);
        var stock = await _repository.GetBranchStockAsync(branchId, ct);
        var finishedGoods = await _productStockLedger.GetOnHandAsync(
            branchId,
            items.Select(item => item.ProductId).Distinct().ToList(),
            ct);

        var remainingStock = stock.ToDictionary(row => row.Key, row => row.Value);
        var remainingFinishedGoods = finishedGoods.ToDictionary(row => row.Key, row => row.Value);
        foreach (var item in items)
        {
            if (!productLookup.TryGetValue(item.ProductId, out var product))
                return "One or more products are not available at the selected branch.";

            if (!AvailabilityHelper.IsAvailableNow(product.AvailableStartDate, product.AvailableEndDate, product.AvailableFrom, product.AvailableTo))
                return $"{product.Name} is unavailable now.";
            if (remainingFinishedGoods.TryGetValue(item.ProductId, out var onHand))
            {
                if (onHand < item.Quantity)
                    return $"{product.Name} is out of stock.";
                remainingFinishedGoods[item.ProductId] = onHand - item.Quantity;
                continue;
            }

            var requirements = ResolveRequirements(product, item.SelectedOptionId, item.Modifiers);
            if (!CanFulfil(requirements, remainingStock, item.Quantity))
                return $"{product.Name} is out of stock.";
            foreach (var requirement in requirements)
                remainingStock[requirement.Key] = remainingStock.GetValueOrDefault(requirement.Key) - requirement.Value * item.Quantity;
        }

        return null;
    }

    public async Task<OnlineShoppingOrderStatusDto> GetOrderStatusAsync(
        Guid orderId,
        string clientOrderUuid,
        CancellationToken ct)
    {
        var order = await _repository.GetOnlineOrderAsync(orderId, clientOrderUuid.Trim(), ct)
            ?? throw new NotFoundException("Online order was not found.");
        var paidAmount = OrderPaymentHelper.RoundCurrency(order.PaidAmount);
        var remainingAmount = OrderPaymentHelper.RoundCurrency(
            Math.Max(0m, order.TotalAmount - paidAmount));
        var isPaid = order.PaidAt.HasValue ||
            order.Status is OrderStatus.Paid or OrderStatus.Completed ||
            remainingAmount <= 0m;
        var orderType = Enum.Parse<OrderType>(order.OrderType);
        return new OnlineShoppingOrderStatusDto
        {
            OrderId = order.OrderId,
            OrderNumber = order.OrderNumber,
            OrderType = order.OrderType,
            Status = order.Status.ToString(),
            CustomerStatus = OnlineOrderStatusMapper.GetCustomerStatus(
                order.Status,
                orderType,
                order.AllItemsReady,
                order.IsRefunded,
                order.DispatchedAt),
            CreatedAt = order.CreatedAt,
            TotalAmount = order.TotalAmount,
            PaidAmount = paidAmount,
            RemainingAmount = remainingAmount,
            IsPaid = isPaid,
            PaymentStatus = ResolvePaymentStatus(order, paidAmount, isPaid),
            PaymentMethod = order.PaymentMethodName ??
                (OrderPaymentHelper.IsCashMethod(order.PaymentMethod)
                    ? OrderPaymentHelper.CashLabelEn
                    : order.PaymentMethod ?? string.Empty),
            PaymentMethodAr = order.PaymentMethodNameAr ??
                (OrderPaymentHelper.IsCashMethod(order.PaymentMethod)
                    ? OrderPaymentHelper.CashLabelAr
                    : null),
            SubmittedPaymentReference = order.SubmittedPaymentReference,
            ScheduledFor = order.ScheduledFor,
            DispatchedAt = order.DispatchedAt,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            CustomerPhone = order.CustomerPhone,
            DeliveryAddress = order.DeliveryAddress,
            DeliveryNotes = order.DeliveryNotes,
            DeliveryZoneName = order.DeliveryZoneName,
            DeliveryFee = order.DeliveryFee,
            Branch = order.Branch,
            Items = order.Items
        };
    }

    public async Task<OnlineStoreOrderLookupResultDto> LookupOrderAsync(
        string orderNumber,
        string contact,
        CancellationToken ct)
    {
        var number = RequireText(orderNumber, "Order number");
        var proof = RequireText(contact, "Contact details");
        var candidates = await _repository.FindOnlineOrdersByNumberAsync(number, ct);
        // One generic failure for "no such order" and "contact does not match" so the
        // endpoint cannot be used to discover which order numbers exist.
        var match = candidates.FirstOrDefault(row => MatchesContact(row, proof))
            ?? throw new NotFoundException(OrderLookupFailureMessage);
        return new OnlineStoreOrderLookupResultDto
        {
            TrackingToken = match.ClientOrderUuid,
            Order = await GetOrderStatusAsync(match.OrderId, match.ClientOrderUuid, ct)
        };
    }

    private static bool MatchesContact(OnlineOrderLookupRow row, string contact)
        => MatchesEmail(row.CustomerEmail, contact) || MatchesPhone(row.CustomerPhone, contact);

    private static bool MatchesEmail(string? stored, string contact)
        => !string.IsNullOrWhiteSpace(stored) &&
            string.Equals(stored.Trim(), contact, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesPhone(string? stored, string contact)
    {
        var storedDigits = DigitsOnly(stored);
        var providedDigits = DigitsOnly(contact);
        if (storedDigits.Length < MinimumPhoneDigits || providedDigits.Length < MinimumPhoneDigits)
            return false;
        // Customers type the same number with or without country code / leading zero,
        // so the shorter form is accepted when it is the tail of the stored number.
        return storedDigits == providedDigits ||
            storedDigits.EndsWith(providedDigits, StringComparison.Ordinal) ||
            providedDigits.EndsWith(storedDigits, StringComparison.Ordinal);
    }

    private static string DigitsOnly(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(char.IsDigit).ToArray());

    private static OnlineShoppingProductAvailabilityDto MapAvailability(
        Product product,
        IReadOnlyDictionary<Guid, decimal> stock,
        IReadOnlyDictionary<Guid, decimal> finishedGoods)
    {
        if (finishedGoods.TryGetValue(product.Id, out var onHand))
            return MapFinishedGoodsAvailability(product, onHand);

        var requirements = ResolveRequirements(product, null, []);
        var tracked = requirements.Count > 0 || product.Options.Any(option => option.RecipeItems.Count > 0);
        var quantity = ResolveAvailableQuantity(requirements, stock);
        var options = product.Options.Where(option => option.IsActive).OrderBy(option => option.SortOrder).ToList();
        var anyOptionAvailable = options.Any(option => CanFulfil(ToRequirements(option.RecipeItems), stock, 1));
        var inStock = !tracked || quantity.GetValueOrDefault() > 0 || anyOptionAvailable;

        return new OnlineShoppingProductAvailabilityDto
        {
            ProductId = product.Id,
            IsAvailableOnline = inStock && AvailabilityHelper.IsAvailableNow(
                product.AvailableStartDate,
                product.AvailableEndDate,
                product.AvailableFrom,
                product.AvailableTo),
            InventoryTrackingEnabled = tracked,
            AvailableQuantity = tracked && options.Count == 0 ? quantity : null
        };
    }

    private static OnlineShoppingProductAvailabilityDto MapFinishedGoodsAvailability(Product product, decimal onHand)
    {
        var available = (int)Math.Floor(Math.Max(0m, onHand));
        return new OnlineShoppingProductAvailabilityDto
        {
            ProductId = product.Id,
            IsAvailableOnline = available > 0 && AvailabilityHelper.IsAvailableNow(
                product.AvailableStartDate,
                product.AvailableEndDate,
                product.AvailableFrom,
                product.AvailableTo),
            InventoryTrackingEnabled = true,
            AvailableQuantity = available
        };
    }

    private static Dictionary<Guid, decimal> ResolveRequirements(
        Product product,
        Guid? optionId,
        IReadOnlyList<OrderItemModifierCreateDto> modifiers)
    {
        if (optionId.HasValue)
        {
            var option = product.Options.FirstOrDefault(candidate => candidate.Id == optionId && candidate.IsActive);
            return option == null ? [] : ToRequirements(option.RecipeItems);
        }

        var selectedIds = modifiers.Where(modifier => modifier.ModifierId.HasValue)
            .Select(modifier => modifier.ModifierId!.Value)
            .ToHashSet();
        var selectedModifiers = product.ProductModifierGroups
            .SelectMany(link => link.ModifierGroup.Modifiers)
            .Where(modifier => selectedIds.Contains(modifier.Id))
            .ToList();
        var modifierRequirements = ToRequirements(selectedModifiers.SelectMany(modifier => modifier.RecipeItems));
        return modifierRequirements.Count > 0 ? modifierRequirements : ToRequirements(product.RecipeItems);
    }

    private static Dictionary<Guid, decimal> ToRequirements(IEnumerable<RecipeItem> rows)
        => AggregateRequirements(rows.Select(row => (row.RawMaterialId, row.Amount)));

    private static Dictionary<Guid, decimal> ToRequirements(IEnumerable<ProductOptionRecipeItem> rows)
        => AggregateRequirements(rows.Select(row => (row.RawMaterialId, row.Amount)));

    private static Dictionary<Guid, decimal> ToRequirements(IEnumerable<ModifierRecipeItem> rows)
        => AggregateRequirements(rows.Select(row => (row.RawMaterialId, row.Amount)));

    private static Dictionary<Guid, decimal> AggregateRequirements(
        IEnumerable<(Guid MaterialId, decimal Amount)> rows)
        => rows.GroupBy(row => row.MaterialId)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.Amount));

    private static bool CanFulfil(
        IReadOnlyDictionary<Guid, decimal> requirements,
        IReadOnlyDictionary<Guid, decimal> stock,
        int quantity)
        => requirements.All(row => stock.GetValueOrDefault(row.Key) >= row.Value * quantity);

    private static int? ResolveAvailableQuantity(
        IReadOnlyDictionary<Guid, decimal> requirements,
        IReadOnlyDictionary<Guid, decimal> stock)
        => requirements.Count == 0
            ? null
            : requirements.Min(row => row.Value <= 0m
                ? int.MaxValue
                : (int)Math.Floor(stock.GetValueOrDefault(row.Key) / row.Value));

    private static OnlineShoppingBranchDto MapBranch(Branch branch) => new()
    {
        Id = branch.Id,
        Name = branch.Name,
        NameAr = branch.NameAr,
        Code = branch.Code,
        Address = branch.Address,
        Phone = branch.Phone,
        IsMainBranch = branch.IsMainBranch
    };

    private static OnlineShoppingPaymentMethodDto MapPaymentMethod(PaymentMethod method)
    {
        var isCash = OrderPaymentHelper.IsCashMethod(method.Code)
            || OrderPaymentHelper.IsCashMethod(method.NameEn)
            || OrderPaymentHelper.IsCashMethod(method.NameAr);
        return new OnlineShoppingPaymentMethodDto
        {
            Id = method.Id,
            Name = method.NameEn,
            NameAr = method.NameAr,
            Code = method.Code,
            Icon = method.Icon,
            DisplayOrder = method.DisplayOrder,
            RequiresReferenceNumber = method.RequiresReferenceNumber,
            IsCash = isCash,
            IsCard = !isCash && (OrderPaymentHelper.IsCardMethod(method.Code)
                || OrderPaymentHelper.IsCardMethod(method.NameEn)
                || OrderPaymentHelper.IsCardMethod(method.NameAr)),
            IsOnline = !isCash
        };
    }

    /// <summary>
    /// Every payment method the merchant enabled for this branch, in their configured order.
    /// The repository has already applied BranchPaymentMethod and the active flag, so nothing
    /// is filtered here: which methods a shopper may pick is a configuration decision, not a
    /// storefront one.
    /// </summary>
    private static IReadOnlyList<OnlineShoppingPaymentMethodDto> MapPaymentMethods(
        IEnumerable<PaymentMethod> methods)
    {
        return methods.Select(MapPaymentMethod).ToList();
    }

    private static string ResolvePaymentStatus(
        OnlineShoppingOrderRow order,
        decimal paidAmount,
        bool isPaid)
        => order.IsRefunded
            ? OrderPaymentHelper.RefundedStatus
            : order.Status == OrderStatus.PaymentCancelled
                ? OrderPaymentHelper.FailedStatus
                : isPaid
                    ? OrderPaymentHelper.PaidStatus
                    : paidAmount > 0m
                        ? OrderPaymentHelper.PartialStatus
                        : OrderPaymentHelper.PendingStatus;

    private static string RequireText(string value, string field)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ValidationException($"{field} is required.")
            : value.Trim();

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
