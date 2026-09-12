using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services;

public sealed partial class OnlineShoppingService
{
    private const int StoreCustomerNameMaxLength = 120;
    private const int StoreAddressLineMaxLength = 500;
    private const int StoreBuildingMaxLength = 120;
    private const int StoreStreetMaxLength = 160;
    private const int StoreFloorMaxLength = 120;
    private const int StorePaymentReferenceMaxLength = 120;
    public async Task<OrderCreateDto> PrepareStoreOrderAsync(
        OrderCreateDto input, Guid tenantId, Guid branchId, CancellationToken ct)
    {
        ValidateStoreContact(input);
        var methods = await _repository.GetPaymentMethodsAsync(branchId, ct);
        var method = MapPaymentMethods(methods).FirstOrDefault(row =>
            string.Equals(row.Code, input.PaymentMethod, StringComparison.OrdinalIgnoreCase))
            ?? throw new ValidationException("The selected payment method is unavailable.");
        var paymentReference = ResolvePaymentReference(method, input.PaymentReferenceNumber);
        var products = (await _repository.GetBranchProductsAsync(tenantId, branchId, ct))
            .ToDictionary(product => product.Id);
        var items = input.Items.Select(item => PrepareStoreItem(item, products)).ToList();
        var availabilityError = await ValidateAvailabilityAsync(tenantId, branchId, items, ct);
        if (availabilityError != null) throw new ValidationException(availabilityError);
        // Offer lines survive into the order, so the offer must still be live for this branch
        // and the lines must be whole copies of the bundle. Order intake then re-derives every
        // unit price from the offer itself; the client's numbers were already discarded above.
        await _offerService.ValidateOfferLinesAsync(tenantId, branchId, BuildOfferLines(items), ct);
        return BuildStoreOrder(input, tenantId, method.Code, paymentReference, items);
    }

    private static void ValidateStoreContact(OrderCreateDto input)
    {
        if (input.OrderType != (int)OrderType.Takeaway && input.OrderType != (int)OrderType.Delivery)
            throw new ValidationException("Choose pickup or delivery.");
        if (input.Items == null || input.Items.Count == 0 || input.Items.Any(item => item == null || item.Quantity <= 0 || item.Modifiers == null))
            throw new ValidationException("Choose a valid product quantity.");
        if (string.IsNullOrWhiteSpace(input.CustomerName) || input.CustomerName.Trim().Length > StoreCustomerNameMaxLength || string.IsNullOrWhiteSpace(input.CustomerPhone))
            throw new ValidationException("Customer name and phone are required.");
        if (!string.IsNullOrWhiteSpace(input.CustomerEmail) &&
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(input.CustomerEmail))
            throw new ValidationException("Enter a valid email address.");
        if (input.OrderType == (int)OrderType.Delivery)
            ValidateStoreDeliveryAddress(input);
        if (!Guid.TryParse(input.ClientOrderUuid, out _))
            throw new ValidationException("A valid order access token is required.");
    }

    /// <summary>
    /// Applies the merchant's own <c>PaymentMethod.RequiresReferenceNumber</c> rule. A method
    /// configured to need a transfer/transaction reference cannot be used without one; a method
    /// configured not to need one never stores whatever the client happened to send.
    /// </summary>
    private static string? ResolvePaymentReference(
        OnlineShoppingPaymentMethodDto method,
        string? reference)
    {
        if (!method.RequiresReferenceNumber)
            return null;
        RequireLength(reference, StorePaymentReferenceMaxLength, $"A reference number for {method.Name}");
        return reference!.Trim();
    }

    /// <summary>
    /// The delivery address a shopper must give the store. The area itself is a
    /// <see cref="DeliveryZone"/> and is validated by order intake, which owns the fee.
    /// </summary>
    private static void ValidateStoreDeliveryAddress(OrderCreateDto input)
    {
        RequireLength(input.DeliveryAddress, StoreAddressLineMaxLength, "Delivery address");
        RequireLength(input.DeliveryBuildingNo, StoreBuildingMaxLength, "Building / villa number");
        RequireLength(input.DeliveryStreet, StoreStreetMaxLength, "Street");
        if (!string.IsNullOrWhiteSpace(input.DeliveryFloor) && input.DeliveryFloor.Trim().Length > StoreFloorMaxLength)
            throw new ValidationException("Floor / apartment is too long.");
    }

    private static void RequireLength(string? value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException($"{field} is required.");
        if (value.Trim().Length > maxLength)
            throw new ValidationException($"{field} is too long.");
    }

    /// <summary>
    /// One readable line built from the parts the shopper filled in, so every existing staff
    /// surface — the POS delivery board, the printed ticket, the public receipt — keeps showing
    /// a complete address without a single change. The parts are stored alongside it.
    /// </summary>
    private static string? ComposeDeliveryAddress(OrderCreateDto input)
    {
        var line = string.Join(", ", new[]
            {
                Normalize(input.DeliveryBuildingNo),
                Normalize(input.DeliveryStreet),
                Normalize(input.DeliveryFloor),
                Normalize(input.DeliveryAddress)
            }
            .Where(part => !string.IsNullOrEmpty(part)));
        return string.IsNullOrEmpty(line)
            ? null
            : line.Length > StoreAddressLineMaxLength ? line[..StoreAddressLineMaxLength] : line;
    }

    private static OrderItemCreateDto PrepareStoreItem(OrderItemCreateDto item, IReadOnlyDictionary<Guid, Product> products)
    {
        if (!products.TryGetValue(item.ProductId, out var product))
            throw new ValidationException("A selected product is unavailable.");
        var options = product.Options.Where(option => option.IsActive).ToList();
        if ((options.Count > 0 && !item.SelectedOptionId.HasValue) ||
            (item.SelectedOptionId.HasValue && !options.Any(option => option.Id == item.SelectedOptionId)))
            throw new ValidationException($"Choose an available option for {product.Name}.");
        ValidateStoreModifiers(product, item.Modifiers);
        return new OrderItemCreateDto
        {
            ProductId = product.Id, ProductName = product.Name, Quantity = item.Quantity,
            SelectedOptionId = item.SelectedOptionId, Notes = Normalize(item.Notes),
            // Carried, never trusted: it only says which offer to re-price this line from.
            OfferLineId = item.OfferLineId,
            Modifiers = item.Modifiers.Select(modifier => new OrderItemModifierCreateDto
            {
                ModifierId = modifier.ModifierId, Quantity = modifier.Quantity,
                ModifierName = string.Empty
            }).ToList()
        };
    }

    private static void ValidateStoreModifiers(Product product, IReadOnlyList<OrderItemModifierCreateDto> modifiers)
    {
        var groups = product.ProductModifierGroups.Select(link => link.ModifierGroup).ToList();
        var definitions = groups.SelectMany(group => group.Modifiers).Where(modifier => modifier.IsActive).ToList();
        if (modifiers.Select(modifier => modifier.ModifierId).Distinct().Count() != modifiers.Count ||
            modifiers.Any(row => row.Quantity <= 0 || !definitions.Any(definition =>
                definition.Id == row.ModifierId && (definition.MaxQuantity <= 0 || row.Quantity <= definition.MaxQuantity))))
            throw new ValidationException($"Choose valid options for {product.Name}.");
        foreach (var group in groups)
        {
            var count = modifiers.Count(row => group.Modifiers.Any(modifier => modifier.Id == row.ModifierId));
            if (count < Math.Max(group.MinSelection, group.IsRequired ? 1 : 0) ||
                (group.MaxSelection > 0 && count > group.MaxSelection) ||
                (group.SelectionType == SelectionType.Single && count > 1))
                throw new ValidationException($"Check the selected options for {product.Name}.");
        }
    }

    private static List<OfferOrderLine> BuildOfferLines(IReadOnlyList<OrderItemCreateDto> items)
        => items
            .Where(item => item.OfferLineId.HasValue)
            .Select(item => new OfferOrderLine(item.OfferLineId!.Value, item.ProductId, item.Quantity))
            .ToList();

    private static OrderCreateDto BuildStoreOrder(
        OrderCreateDto input,
        Guid tenantId,
        string method,
        string? paymentReference,
        List<OrderItemCreateDto> items) => new()
    {
        TenantId = tenantId, OrderSource = nameof(OrderSource.Online), OrderType = input.OrderType,
        TableName = input.OrderType == (int)OrderType.Delivery ? "ONLINE DELIVERY" : "ONLINE PICKUP",
        ClientOrderUuid = input.ClientOrderUuid, CustomerName = input.CustomerName?.Trim(),
        CustomerPhone = input.CustomerPhone?.Trim(), CustomerEmail = Normalize(input.CustomerEmail),
        PaymentMethod = method, PaymentReferenceNumber = paymentReference,
        ScheduledFor = input.ScheduledFor,
        DeliveryZoneId = input.OrderType == (int)OrderType.Delivery ? input.DeliveryZoneId : null,
        DeliveryAddress = input.OrderType == (int)OrderType.Delivery ? ComposeDeliveryAddress(input) : null,
        DeliveryNotes = Normalize(input.DeliveryNotes), Items = items
    };
}
