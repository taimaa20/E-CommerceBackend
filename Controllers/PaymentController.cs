using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Modules.Payments.Api;
using RestaurantPos.Api.Modules.Payments.Application.CreatePayment;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers;

[ApiController]
[Route("api/payment-engine/payments")]
[Route("api/v1/payment-engine/payments")]
[Produces("application/json")]
public sealed class PaymentController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ITenantResolver _tenantResolver;
    private readonly IBranchContext _branchContext;
    private readonly ICurrentUserAccessor _currentUser;

    public PaymentController(
        ISender sender,
        ITenantResolver tenantResolver,
        IBranchContext branchContext,
        ICurrentUserAccessor currentUser)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
        _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    [HttpPost]
    [Authorize(Roles = AppRoleGroups.TakeawayCheckoutOperators)]
    [ProducesResponseType(typeof(CreatePaymentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatePaymentResult>> CreatePayment(
        [FromBody] CreatePaymentRequestDto? request,
        CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Request body is required." });
        }

        var branchContext = await _branchContext.GetCurrentAsync(ct);
        var command = MapToCommand(
            request,
            _tenantResolver.GetTenantId(),
            branchContext.CurrentBranch.Id,
            _currentUser.UserIdOrNull,
            ResolveCorrelationId(request));

        var result = await _sender.Send(command, ct);
        return Ok(result);
    }

    private string ResolveCorrelationId(CreatePaymentRequestDto request)
        => string.IsNullOrWhiteSpace(request.CorrelationId)
            ? HttpContext.TraceIdentifier
            : request.CorrelationId.Trim();

    private static CreatePaymentCommand MapToCommand(
        CreatePaymentRequestDto request,
        Guid tenantId,
        Guid branchId,
        Guid? createdByUserId,
        string correlationId)
    {
        return new CreatePaymentCommand
        {
            TenantId = tenantId,
            BranchId = branchId,
            OrderId = request.OrderId,
            MerchantReference = request.MerchantReference,
            Amount = request.Amount,
            Currency = request.Currency,
            Method = request.Method,
            ProviderCode = request.ProviderCode,
            IdempotencyKey = request.IdempotencyKey,
            PaymentMethods = [],
            BillingData = MapBillingData(request.BillingData),
            Customer = request.Customer is null ? null : MapCustomerData(request.Customer),
            Items = request.Items.Select(MapLineItem).ToArray(),
            NotificationUrl = null,
            RedirectionUrl = null,
            ExpirationSeconds = null,
            CreatedByUserId = createdByUserId,
            CorrelationId = correlationId
        };
    }

    private static CreatePaymentBillingData MapBillingData(CreatePaymentBillingDataDto data)
    {
        return new CreatePaymentBillingData
        {
            FirstName = data.FirstName,
            LastName = data.LastName,
            Email = data.Email,
            PhoneNumber = data.PhoneNumber,
            Country = data.Country,
            City = data.City,
            State = data.State,
            PostalCode = data.PostalCode,
            Street = data.Street,
            Building = data.Building,
            Floor = data.Floor,
            Apartment = data.Apartment
        };
    }

    private static CreatePaymentCustomerData MapCustomerData(CreatePaymentCustomerDataDto data)
    {
        return new CreatePaymentCustomerData
        {
            FirstName = data.FirstName,
            LastName = data.LastName,
            Email = data.Email
        };
    }

    private static CreatePaymentLineItem MapLineItem(CreatePaymentLineItemDto item)
    {
        return new CreatePaymentLineItem
        {
            Name = item.Name,
            Amount = item.Amount,
            Quantity = item.Quantity,
            Description = item.Description,
            ImageUrl = item.ImageUrl
        };
    }
}
