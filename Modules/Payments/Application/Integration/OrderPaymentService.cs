using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Services;
using LegacyPayment = RestaurantPos.Api.Models.Payment;

namespace RestaurantPos.Api.Modules.Payments.Application.Integration;

public sealed class OrderPaymentService : IOrderPaymentService
{
    private const decimal AmountTolerance = 0.01m;

    private readonly IPaymentRepository _paymentRepository;
    private readonly ISettingsService _settingsService;
    private readonly IOrderPreparationCoordinator _preparationCoordinator;
    private readonly ILogger<OrderPaymentService> _logger;

    public OrderPaymentService(
        IPaymentRepository paymentRepository,
        ISettingsService settingsService,
        IOrderPreparationCoordinator preparationCoordinator,
        ILogger<OrderPaymentService> logger)
    {
        _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _preparationCoordinator = preparationCoordinator ?? throw new ArgumentNullException(nameof(preparationCoordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ApplyCompletedPaymentAsync(PaymentCompletedDomainEvent paymentEvent, CancellationToken ct)
    {
        var order = await LoadVerifiedOrderAsync(paymentEvent, ct);
        if (HasPaymentReference(order, paymentEvent))
        {
            if (!OrderPaymentHelper.BuildSnapshot(order).IsPaid)
            {
                EnsureOrderMarkedPaid(order);
                await _paymentRepository.UpdateOrderPaymentFieldsAsync(order, ct);
            }

            return;
        }

        EnsureOrderCanReceivePayment(order);

        var payment = CreateLegacyPayment(paymentEvent);
        order.Payments.Add(payment);
        order.PaymentMethod = BuildPaymentMethodSummary(order, paymentEvent);
        order.PaidAt = paymentEvent.OccurredAt;
        order.Status = OrderStatus.Paid;
        order.SyncedAt = DateTime.UtcNow;

        await _paymentRepository.UpdateOrderPaymentFieldsAsync(order, ct);
        await _paymentRepository.AddPaymentRecordAsync(payment, ct);
        await _preparationCoordinator.StartAfterPaymentAsync(order, ct);

        _logger.LogInformation(
            "Applied Payment Engine completion to order. PaymentId {PaymentId}. OrderId {OrderId}. Provider {Provider}",
            paymentEvent.PaymentId,
            paymentEvent.OrderId,
            paymentEvent.Provider);
    }

    public async Task ApplyPendingPaymentAsync(PaymentPendingDomainEvent paymentEvent, CancellationToken ct)
    {
        var order = await LoadVerifiedOrderAsync(paymentEvent, ct);
        if (OrderPaymentHelper.BuildSnapshot(order).IsPaid)
        {
            return;
        }

        if (order.Status != OrderStatus.PaymentCancelled)
        {
            order.Status = OrderStatus.PendingPayment;
            order.SyncedAt = DateTime.UtcNow;
            await _paymentRepository.UpdateOrderPaymentFieldsAsync(order, ct);
        }
    }

    public async Task ApplyCancelledPaymentAsync(PaymentCancelledDomainEvent paymentEvent, CancellationToken ct)
    {
        var order = await LoadVerifiedOrderAsync(paymentEvent, ct);
        if (OrderPaymentHelper.BuildSnapshot(order).IsPaid)
        {
            return;
        }

        order.Status = OrderStatus.PaymentCancelled;
        order.SyncedAt = DateTime.UtcNow;
        await _paymentRepository.UpdateOrderPaymentFieldsAsync(order, ct);
    }

    private async Task<Order> LoadVerifiedOrderAsync(PaymentCompletedDomainEvent paymentEvent, CancellationToken ct)
        => await LoadVerifiedOrderAsync(
            paymentEvent.OrderId,
            paymentEvent.TenantId,
            paymentEvent.BranchId,
            paymentEvent.Amount,
            paymentEvent.Currency,
            paymentEvent.PaymentId,
            ct);

    private async Task<Order> LoadVerifiedOrderAsync(PaymentPendingDomainEvent paymentEvent, CancellationToken ct)
        => await LoadVerifiedOrderAsync(
            paymentEvent.OrderId,
            paymentEvent.TenantId,
            paymentEvent.BranchId,
            paymentEvent.Amount,
            paymentEvent.Currency,
            paymentEvent.PaymentId,
            ct);

    private async Task<Order> LoadVerifiedOrderAsync(PaymentCancelledDomainEvent paymentEvent, CancellationToken ct)
        => await LoadVerifiedOrderAsync(
            paymentEvent.OrderId,
            paymentEvent.TenantId,
            paymentEvent.BranchId,
            paymentEvent.Amount,
            paymentEvent.Currency,
            paymentEvent.PaymentId,
            ct);

    private async Task<Order> LoadVerifiedOrderAsync(
        Guid orderId,
        Guid tenantId,
        Guid branchId,
        decimal amount,
        string currency,
        Guid paymentId,
        CancellationToken ct)
    {
        await _paymentRepository.LockOrderRowAsync(orderId, ct);
        var order = await _paymentRepository.GetOrderForPaymentIntegrationAsync(orderId, ct)
            ?? throw new NotFoundException("Order", orderId);

        if (order.TenantId != tenantId)
        {
            throw new ValidationException("Payment tenant does not match the order tenant.");
        }

        if (order.BranchId != branchId)
        {
            throw new ValidationException("Payment branch does not match the order branch.");
        }

        if (Math.Abs(OrderPaymentHelper.RoundCurrency(order.TotalAmount) - OrderPaymentHelper.RoundCurrency(amount)) > AmountTolerance)
        {
            throw new ValidationException("Payment amount does not match the order total.");
        }

        var settings = await _settingsService.GetSettingsAsync(tenantId, ct);
        if (!string.Equals(NormalizeCurrency(settings.Currency), NormalizeCurrency(currency), StringComparison.Ordinal))
        {
            throw new ValidationException("Payment currency does not match the restaurant currency.");
        }

        _logger.LogDebug(
            "Verified order payment integration. PaymentId {PaymentId}. OrderId {OrderId}. TenantId {TenantId}. BranchId {BranchId}",
            paymentId,
            orderId,
            tenantId,
            branchId);

        return order;
    }

    private static void EnsureOrderCanReceivePayment(Order order)
    {
        if (order.Status is OrderStatus.Cancelled or OrderStatus.Completed)
        {
            throw new ValidationException("Order cannot be paid in its current state.");
        }
    }

    private static void EnsureOrderMarkedPaid(Order order)
    {
        order.Status = OrderStatus.Paid;
        order.PaidAt ??= DateTime.UtcNow;
        order.PaymentMethod ??= OrderPaymentHelper.BuildPaymentDisplaySummary(order.Payments);
        order.SyncedAt = DateTime.UtcNow;
    }

    private static bool HasPaymentReference(Order order, PaymentCompletedDomainEvent paymentEvent)
    {
        var reference = ResolveReference(paymentEvent);
        return order.Payments.Any(payment =>
            string.Equals(payment.ReferenceNumber, reference, StringComparison.Ordinal) &&
            string.Equals(payment.PaymentMethodCode, NormalizeProviderCode(paymentEvent.Provider), StringComparison.Ordinal));
    }

    private static LegacyPayment CreateLegacyPayment(PaymentCompletedDomainEvent paymentEvent)
    {
        var providerCode = NormalizeProviderCode(paymentEvent.Provider);
        var providerLabel = ResolveProviderLabel(paymentEvent.Provider);
        var occurredAt = EnsureUtc(paymentEvent.OccurredAt);

        return new LegacyPayment
        {
            Id = Guid.NewGuid(),
            TenantId = paymentEvent.TenantId,
            BranchId = paymentEvent.BranchId,
            OrderId = paymentEvent.OrderId,
            Amount = OrderPaymentHelper.RoundCurrency(paymentEvent.Amount),
            Method = providerLabel,
            PaymentMethodName = providerLabel,
            PaymentMethodCode = providerCode,
            ReferenceNumber = ResolveReference(paymentEvent),
            CreatedAt = occurredAt
        };
    }

    private string BuildPaymentMethodSummary(Order order, PaymentCompletedDomainEvent paymentEvent)
    {
        return OrderPaymentHelper.BuildPaymentMethodSummary(
            order.Payments.Select(payment => payment.Method))
            ?? ResolveProviderLabel(paymentEvent.Provider);
    }

    private static string ResolveReference(PaymentCompletedDomainEvent paymentEvent)
        => string.IsNullOrWhiteSpace(paymentEvent.Reference)
            ? paymentEvent.PaymentId.ToString("N")
            : paymentEvent.Reference.Trim();

    private static string ResolveProviderLabel(string provider)
    {
        var normalized = NormalizeProviderCode(provider);
        return normalized.Length == 0
            ? "Payment Engine"
            : char.ToUpperInvariant(normalized[0]) + normalized[1..];
    }

    private static string NormalizeProviderCode(string provider)
        => string.IsNullOrWhiteSpace(provider)
            ? string.Empty
            : provider.Trim().ToUpperInvariant();

    private static string NormalizeCurrency(string currency)
        => string.IsNullOrWhiteSpace(currency)
            ? string.Empty
            : currency.Trim().ToUpperInvariant();

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
