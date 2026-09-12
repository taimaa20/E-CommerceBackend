using MediatR;
using Microsoft.Extensions.Options;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.CustomerMobile.Repositories;
using RestaurantPos.Api.Modules.Payments.Application.CreatePayment;
using RestaurantPos.Api.Modules.Payments.Domain.Enums;
using RestaurantPos.Api.Modules.Payments.Infrastructure.Paymob;
using RestaurantPos.Api.Services;
using EnginePaymentMethod = RestaurantPos.Api.Modules.Payments.Domain.Enums.PaymentMethod;

namespace RestaurantPos.Api.Modules.CustomerMobile.Application.Checkout;

public sealed class CreateCustomerCheckoutCommandHandler
    : IRequestHandler<CreateCustomerCheckoutCommand, CreatePaymentResult>
{
    private const string SupportedAction = "order";
    private const string EnglishLanguage = "en";
    private const string ArabicLanguage = "ar";
    private const string CashPaymentCode = "CASH";
    private const string CashPaymentName = "Cash";
    private const string CashPaymentNameAr = "نقدي";
    private const string IdempotencyPrefix = "mobile-checkout";
    private const string MerchantReferencePrefix = "MOBILE";
    private static readonly string[] WalletPaymentMarkers = ["WALLET", "VODAFONE", "ETISALAT", "ORANGE"];
    private static readonly string[] BankTransferPaymentMarkers = ["BANK", "INSTAPAY", "CLIQ"];

    private readonly ICustomerCheckoutRepository _repository;
    private readonly ISettingsService _settingsService;
    private readonly IOptionsMonitor<PaymobOptions> _paymobOptions;
    private readonly ISender _sender;

    public CreateCustomerCheckoutCommandHandler(
        ICustomerCheckoutRepository repository,
        ISettingsService settingsService,
        IOptionsMonitor<PaymobOptions> paymobOptions,
        ISender sender)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _paymobOptions = paymobOptions ?? throw new ArgumentNullException(nameof(paymobOptions));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public async Task<CreatePaymentResult> Handle(
        CreateCustomerCheckoutCommand request,
        CancellationToken ct)
    {
        ValidateRequest(request);
        var order = await LoadEligibleOrderAsync(request, ct);
        var settings = await _settingsService.GetSettingsAsync(request.TenantId, ct);
        var paymentCommand = BuildPaymentCommand(request, order, settings.Currency);
        return await _sender.Send(paymentCommand, ct);
    }

    private async Task<CustomerCheckoutOrder> LoadEligibleOrderAsync(
        CreateCustomerCheckoutCommand request,
        CancellationToken ct)
    {
        var order = await _repository.GetOwnedOrderAsync(
            request.TenantId,
            request.CustomerId,
            request.OrderId,
            ct) ?? throw new NotFoundException("Customer order was not found.");

        ValidateEligibility(order);
        return order;
    }

    private CreatePaymentCommand BuildPaymentCommand(
        CreateCustomerCheckoutCommand request,
        CustomerCheckoutOrder order,
        string currency)
    {
        var options = _paymobOptions.CurrentValue;
        return new CreatePaymentCommand
        {
            TenantId = request.TenantId,
            BranchId = order.BranchId,
            OrderId = order.OrderId,
            MerchantReference = $"{MerchantReferencePrefix}-{order.OrderId:N}",
            Amount = order.TotalAmount,
            Currency = currency,
            Method = ResolvePaymentMethod(order.PaymentMethod),
            ProviderCode = PaymobClient.ProviderCode,
            IdempotencyKey = $"{IdempotencyPrefix}:{order.OrderId:N}",
            PaymentMethods = options.PaymentMethods,
            BillingData = BuildBillingData(order, options.DefaultBillingCountry),
            Customer = BuildCustomerData(order),
            Items = [],
            NotificationUrl = options.NotificationUrl,
            RedirectionUrl = options.RedirectionUrl,
            ExpirationSeconds = options.ExpirationSeconds,
            CreatedByUserId = null,
            CorrelationId = request.CorrelationId
        };
    }

    private static CreatePaymentBillingData BuildBillingData(
        CustomerCheckoutOrder order,
        string country)
        => new()
        {
            FirstName = order.FirstName,
            LastName = order.LastName,
            Email = order.Email,
            PhoneNumber = order.PhoneNumber,
            Country = country,
            Street = order.DeliveryAddress
        };

    private static CreatePaymentCustomerData BuildCustomerData(CustomerCheckoutOrder order)
        => new()
        {
            FirstName = order.FirstName,
            LastName = order.LastName,
            Email = order.Email
        };

    private static void ValidateRequest(CreateCustomerCheckoutCommand request)
    {
        if (request.OrderId == Guid.Empty)
            throw new ValidationException("OrderId is required.");
        if (!string.Equals(request.Action, SupportedAction, StringComparison.Ordinal))
            throw new ValidationException("Action must be 'order'.");
        if (!IsSupportedLanguage(request.Language))
            throw new ValidationException("Language must be 'en' or 'ar'.");
    }

    private static void ValidateEligibility(CustomerCheckoutOrder order)
    {
        if (!IsActive(order.Status))
            throw new ValidationException("Order is not eligible for payment.");
        if (order.PaidAt.HasValue || order.HasRecordedPayment)
            throw new ValidationException("Order has already been paid.");
        if (order.TotalAmount <= 0m)
            throw new ValidationException("Order total must be greater than zero.");
        if (IsCashPayment(order.PaymentMethod))
            throw new ValidationException("Cash orders do not require hosted checkout.");
    }

    private static EnginePaymentMethod ResolvePaymentMethod(string? paymentMethod)
    {
        var normalized = paymentMethod?.Trim().ToUpperInvariant() ?? string.Empty;
        if (WalletPaymentMarkers.Any(normalized.Contains))
            return EnginePaymentMethod.Wallet;
        if (BankTransferPaymentMarkers.Any(normalized.Contains))
            return EnginePaymentMethod.BankTransfer;
        return EnginePaymentMethod.Card;
    }

    private static bool IsCashPayment(string? paymentMethod)
        => string.Equals(paymentMethod?.Trim(), CashPaymentCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(paymentMethod?.Trim(), CashPaymentName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(paymentMethod?.Trim(), CashPaymentNameAr, StringComparison.Ordinal);

    private static bool IsSupportedLanguage(string language)
        => string.Equals(language, EnglishLanguage, StringComparison.Ordinal)
            || string.Equals(language, ArabicLanguage, StringComparison.Ordinal);

    private static bool IsActive(OrderStatus status)
        => status is OrderStatus.New
            or OrderStatus.Preparing
            or OrderStatus.Ready
            or OrderStatus.Served
            or OrderStatus.PendingPayment
            or OrderStatus.PaymentCancelled;
}
