using MediatR;
using RestaurantPos.Api.Modules.Payments.Domain.Enums;

namespace RestaurantPos.Api.Modules.Payments.Application.CreatePayment;

public sealed record CreatePaymentCommand : IRequest<CreatePaymentResult>
{
    public Guid TenantId { get; init; }

    public Guid BranchId { get; init; }

    public Guid OrderId { get; init; }

    public required string MerchantReference { get; init; }

    public decimal Amount { get; init; }

    public required string Currency { get; init; }

    public PaymentMethod Method { get; init; } = PaymentMethod.Card;

    public required string ProviderCode { get; init; }

    public required string IdempotencyKey { get; init; }

    public IReadOnlyList<string> PaymentMethods { get; init; } = [];

    public required CreatePaymentBillingData BillingData { get; init; }

    public CreatePaymentCustomerData? Customer { get; init; }

    public IReadOnlyList<CreatePaymentLineItem> Items { get; init; } = [];

    public string? NotificationUrl { get; init; }

    public string? RedirectionUrl { get; init; }

    public int? ExpirationSeconds { get; init; }

    public Guid? CreatedByUserId { get; init; }

    public string? CorrelationId { get; init; }
}

public sealed record CreatePaymentBillingData
{
    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string Email { get; init; }

    public required string PhoneNumber { get; init; }

    public required string Country { get; init; }

    public string? City { get; init; }

    public string? State { get; init; }

    public string? PostalCode { get; init; }

    public string? Street { get; init; }

    public string? Building { get; init; }

    public string? Floor { get; init; }

    public string? Apartment { get; init; }
}

public sealed record CreatePaymentCustomerData
{
    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string Email { get; init; }
}

public sealed record CreatePaymentLineItem
{
    public required string Name { get; init; }

    public decimal Amount { get; init; }

    public int Quantity { get; init; } = 1;

    public string? Description { get; init; }

    public string? ImageUrl { get; init; }
}
