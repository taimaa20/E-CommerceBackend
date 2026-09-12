using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Npgsql;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Modules.Payments.Application.Abstractions;
using RestaurantPos.Api.Modules.Payments.Application.Integration;
using RestaurantPos.Api.Modules.Payments.Application.ProcessWebhook;
using RestaurantPos.Api.Modules.Payments.Domain.Entities;
using RestaurantPos.Api.Modules.Payments.Domain.Enums;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;
using RestaurantPos.Api.Modules.Payments.Infrastructure.Gateways;
using RestaurantPos.Api.Modules.Payments.Interfaces;

namespace RestaurantPos.Api.Modules.Payments.Application.CreatePayment;

public sealed class PaymentOrchestrator : IPaymentOrchestrator
{
    private const int MaxConcurrencyRetries = 3;
    private const string UniqueViolationSqlState = "23505";
    private const string ForeignKeyViolationSqlState = "23503";

    private readonly IPaymentEngineUnitOfWork _unitOfWork;
    private readonly IReadOnlyCollection<IPaymentGateway> _gateways;
    private readonly IDomainEventDispatcher _domainEventDispatcher;
    private readonly ILogger<PaymentOrchestrator> _logger;
    private readonly IHostEnvironment? _environment;

    public PaymentOrchestrator(
        IPaymentEngineUnitOfWork unitOfWork,
        IEnumerable<IPaymentGateway> gateways,
        IDomainEventDispatcher domainEventDispatcher,
        ILogger<PaymentOrchestrator> logger,
        IHostEnvironment? environment = null)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _gateways = gateways?.ToArray() ?? throw new ArgumentNullException(nameof(gateways));
        _domainEventDispatcher = domainEventDispatcher ?? throw new ArgumentNullException(nameof(domainEventDispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment;
    }

    public async Task<CreatePaymentResult> CreatePaymentAsync(CreatePaymentCommand command, CancellationToken ct)
    {
        var context = CreateContext(command);
        var gateway = ResolveGateway(context.ProviderCode);
        var prepared = await PreparePaymentAttemptWithRetryAsync(context, ct);

        if (prepared.DuplicateResult is not null)
        {
            return prepared.DuplicateResult;
        }

        // Transaction boundary: no database transaction may cover this external HTTP call.
        // Local state is prepared and committed first, then provider state is persisted in a new transaction.
        GatewayPaymentResult gatewayResult;
        try
        {
            gatewayResult = await gateway.CreatePaymentIntentionAsync(
                BuildGatewayRequest(context, prepared.PaymentId, prepared.PaymentAttemptId),
                ct);
        }
        catch (PaymentGatewayException ex)
        {
            await RecordGatewayFailureAsync(prepared.PaymentId, prepared.PaymentAttemptId, ex, ct);
            throw MapGatewayException(ex);
        }

        return await PersistGatewaySessionAsync(context, prepared, gatewayResult, ct);
    }

    public async Task<ProcessWebhookResult> ProcessWebhookAsync(
        GatewayWebhookEvent webhookEvent,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(webhookEvent);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var payment = await ResolveWebhookPaymentAsync(webhookEvent, ct);
            if (payment is null)
            {
                await transaction.CommitAsync(ct);
                return ProcessWebhookResult.Rejected(
                    StatusCodes.Status404NotFound,
                    webhookEvent.CorrelationId,
                    "Payment was not found for the webhook event.");
            }

            await _unitOfWork.Payments.LockAsync(payment.Id, ct);

            var duplicate = await TryCreateDuplicateWebhookResultAsync(webhookEvent, payment.TenantId, payment.Id, ct);
            if (duplicate is not null)
            {
                await transaction.CommitAsync(ct);
                return duplicate;
            }

            payment = await _unitOfWork.Payments.GetAggregateAsync(payment.Id, ct)
                ?? throw new ConflictException("Payment could not be reloaded after lock acquisition.");

            var attempt = ResolveWebhookAttempt(payment, webhookEvent);
            if (attempt is null)
            {
                await transaction.CommitAsync(ct);
                return ProcessWebhookResult.Rejected(
                    StatusCodes.Status404NotFound,
                    webhookEvent.CorrelationId,
                    "Payment attempt was not found for the webhook event.");
            }

            var session = ResolveWebhookSession(payment, webhookEvent);
            if (session is null)
            {
                await transaction.CommitAsync(ct);
                return ProcessWebhookResult.Rejected(
                    StatusCodes.Status404NotFound,
                    webhookEvent.CorrelationId,
                    "Payment session was not found for the webhook event.");
            }

            if (IsDuplicateOutcome(payment, attempt, session, webhookEvent))
            {
                await transaction.CommitAsync(ct);
                return ProcessWebhookResult.Duplicate(
                    webhookEvent.CorrelationId,
                    payment.Id,
                    "Webhook outcome was already applied.");
            }

            ApplyWebhookEvent(payment, attempt, session, webhookEvent);
            await DispatchRestaurantOrderIntegrationAsync(payment, webhookEvent, ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            await _domainEventDispatcher.DispatchAsync(payment.Events, ct);

            _logger.LogInformation(
                "Processed payment webhook. PaymentId {PaymentId}. PaymentAttemptId {PaymentAttemptId}. SessionId {SessionId}. ProviderCode {ProviderCode}. EventType {EventType}. CorrelationId {CorrelationId}",
                payment.Id,
                attempt.Id,
                session.Id,
                webhookEvent.ProviderCode,
                webhookEvent.EventType,
                webhookEvent.CorrelationId);

            return ProcessWebhookResult.Processed(webhookEvent.CorrelationId, payment.Id);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await transaction.RollbackAsync(ct);
            return ProcessWebhookResult.Duplicate(
                webhookEvent.CorrelationId,
                webhookEvent.PaymentId,
                "Webhook event was already processed.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            return ProcessWebhookResult.Rejected(
                StatusCodes.Status409Conflict,
                webhookEvent.CorrelationId,
                "Payment changed while processing the webhook. The provider may retry.");
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogWarning(
                ex,
                "Rejected payment webhook because the requested state transition is invalid. EventType {EventType}. CorrelationId {CorrelationId}",
                webhookEvent.EventType,
                webhookEvent.CorrelationId);

            return ProcessWebhookResult.Rejected(
                StatusCodes.Status409Conflict,
                webhookEvent.CorrelationId,
                "Payment webhook cannot be applied to the current payment state.");
        }
        catch (ValidationException ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogWarning(
                ex,
                "Rejected payment webhook because order integration validation failed. EventType {EventType}. CorrelationId {CorrelationId}",
                webhookEvent.EventType,
                webhookEvent.CorrelationId);

            return ProcessWebhookResult.Rejected(
                StatusCodes.Status409Conflict,
                webhookEvent.CorrelationId,
                "Payment webhook could not be applied to the order.");
        }
        catch (NotFoundException ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogWarning(
                ex,
                "Rejected payment webhook because the referenced restaurant order was not found. EventType {EventType}. CorrelationId {CorrelationId}",
                webhookEvent.EventType,
                webhookEvent.CorrelationId);

            return ProcessWebhookResult.Rejected(
                StatusCodes.Status409Conflict,
                webhookEvent.CorrelationId,
                "Payment webhook could not be applied to the order.");
        }
    }

    private async Task<PreparedPaymentAttempt> PreparePaymentAttemptWithRetryAsync(
        PaymentCreationContext context,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            try
            {
                return await PreparePaymentAttemptAsync(context, ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                LogPreparationRetry(context, attempt);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex) && attempt < MaxConcurrencyRetries)
            {
                LogPreparationRetry(context, attempt);
            }
        }

        throw new ConflictException("Payment creation conflicted with another request. Retry with the same idempotency key.");
    }

    private async Task<PreparedPaymentAttempt> PreparePaymentAttemptAsync(
        PaymentCreationContext context,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var payment = await GetExistingPaymentForUpdateAsync(context, ct);
            if (payment is not null)
            {
                var duplicate = TryCreateDuplicateResult(payment, context, now);
                if (duplicate is not null)
                {
                    await transaction.CommitAsync(ct);
                    return PreparedPaymentAttempt.Duplicate(duplicate);
                }

                ExpireLapsedCheckoutSessions(payment, now);
                EnsureNoActiveAttempt(payment);
            }
            else
            {
                payment = CreateAggregate(context, now);
                await _unitOfWork.Payments.AddAsync(payment, ct);
            }

            var attempt = payment.AddAttempt(PaymentOperation.Purchase, context.Amount, now, context.RequestFingerprintHash);
            attempt.MarkProcessing();

            await _unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            await _domainEventDispatcher.DispatchAsync(payment.Events, ct);

            return PreparedPaymentAttempt.Pending(payment.Id, attempt.Id);
        }
        catch (DbUpdateException ex) when (IsForeignKeyViolation(ex))
        {
            await transaction.RollbackAsync(ct);
            throw new ValidationException("Payment references an order or branch that does not exist.");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private async Task<CreatePaymentResult> PersistGatewaySessionAsync(
        PaymentCreationContext context,
        PreparedPaymentAttempt prepared,
        GatewayPaymentResult gatewayResult,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var payment = await ReloadForUpdateAsync(prepared.PaymentId, ct);
            var attempt = GetAttempt(payment, prepared.PaymentAttemptId);
            var session = payment.CreateSession(
                gatewayResult.GatewayReference,
                gatewayResult.CheckoutUrl,
                now,
                context.ExpiresAtUtc);

            session.RequireAction();
            payment.RecordAttemptRequiresAction(
                attempt,
                gatewayResult.GatewayReference,
                gatewayResult.ProviderCorrelationId,
                now);

            await _unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            await _domainEventDispatcher.DispatchAsync(payment.Events, ct);

            _logger.LogInformation(
                "Created payment checkout session. PaymentId {PaymentId}. PaymentAttemptId {PaymentAttemptId}. SessionId {SessionId}. ProviderCode {ProviderCode}. CorrelationId {CorrelationId}",
                payment.Id,
                attempt.Id,
                session.Id,
                payment.ProviderCode,
                gatewayResult.CorrelationId);

            return CreateResult(payment, session, gatewayResult, isDuplicate: false);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            throw new ConflictException("Payment changed after the provider accepted the checkout request. Retry with the same idempotency key.");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await transaction.RollbackAsync(ct);
            throw new ConflictException("Payment checkout session already exists. Retry with the same idempotency key.");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private async Task RecordGatewayFailureAsync(
        Guid paymentId,
        Guid paymentAttemptId,
        PaymentGatewayException exception,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var payment = await ReloadForUpdateAsync(paymentId, ct);
            var attempt = GetAttempt(payment, paymentAttemptId);
            var reason = MapFailureReason(exception);

            if (reason == PaymentFailureReason.Timeout)
            {
                payment.RecordAttemptTimedOut(attempt, now, retryAfterUtc: null);
            }
            else
            {
                payment.RecordAttemptFailed(attempt, reason, exception.StatusCode?.ToString(), "Gateway could not create checkout session.", now);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            await _domainEventDispatcher.DispatchAsync(payment.Events, ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private async Task<Payment?> ResolveWebhookPaymentAsync(
        GatewayWebhookEvent webhookEvent,
        CancellationToken ct)
    {
        if (webhookEvent.PaymentId is Guid paymentId && paymentId != Guid.Empty)
        {
            return await _unitOfWork.Payments.GetAggregateAsync(paymentId, ct);
        }

        if (webhookEvent.PaymentAttemptId is Guid attemptId && attemptId != Guid.Empty)
        {
            var attempt = await _unitOfWork.Attempts.GetByIdAsync(attemptId, ct);
            if (attempt is not null)
            {
                return await _unitOfWork.Payments.GetAggregateAsync(attempt.PaymentId, ct);
            }
        }

        if (webhookEvent.PaymentSessionId is Guid sessionId && sessionId != Guid.Empty)
        {
            var session = await _unitOfWork.Sessions.GetByIdAsync(sessionId, ct);
            if (session is not null)
            {
                return await _unitOfWork.Payments.GetAggregateAsync(session.PaymentId, ct);
            }
        }

        if (webhookEvent.GatewayReference is not null)
        {
            var attempt = await _unitOfWork.Attempts.GetByGatewayReferenceAsync(webhookEvent.GatewayReference, ct);
            if (attempt is not null)
            {
                return await _unitOfWork.Payments.GetAggregateAsync(attempt.PaymentId, ct);
            }

            var session = await _unitOfWork.Sessions.GetBySessionReferenceAsync(webhookEvent.GatewayReference, ct);
            if (session is not null)
            {
                return await _unitOfWork.Payments.GetAggregateAsync(session.PaymentId, ct);
            }
        }

        return null;
    }

    private async Task<ProcessWebhookResult?> TryCreateDuplicateWebhookResultAsync(
        GatewayWebhookEvent webhookEvent,
        Guid tenantId,
        Guid? paymentId,
        CancellationToken ct)
    {
        if (await _unitOfWork.Events.ExternalEventExistsAsync(
            tenantId,
            webhookEvent.ProviderCode,
            webhookEvent.ExternalEventId,
            ct))
        {
            return ProcessWebhookResult.Duplicate(
                webhookEvent.CorrelationId,
                paymentId,
                "External webhook event was already processed.");
        }

        var payloadEvents = await _unitOfWork.Events.GetByPayloadHashAsync(
            tenantId,
            webhookEvent.PayloadHash,
            ct);

        return payloadEvents.Count == 0
            ? null
            : ProcessWebhookResult.Duplicate(
                webhookEvent.CorrelationId,
                paymentId,
                "Webhook payload was already processed.");
    }

    private static PaymentAttempt? ResolveWebhookAttempt(
        Payment payment,
        GatewayWebhookEvent webhookEvent)
    {
        if (webhookEvent.PaymentAttemptId is Guid attemptId && attemptId != Guid.Empty)
        {
            return payment.Attempts.FirstOrDefault(attempt => attempt.Id == attemptId);
        }

        if (webhookEvent.GatewayReference is not null)
        {
            var gatewayAttempt = payment.Attempts.FirstOrDefault(attempt =>
                attempt.GatewayReference is not null &&
                GatewayReferencesMatch(attempt.GatewayReference, webhookEvent.GatewayReference));

            if (gatewayAttempt is not null)
            {
                return gatewayAttempt;
            }
        }

        return payment.Attempts
            .OrderByDescending(attempt => attempt.AttemptNumber)
            .FirstOrDefault(attempt => attempt.IsActive);
    }

    private static PaymentSession? ResolveWebhookSession(
        Payment payment,
        GatewayWebhookEvent webhookEvent)
    {
        if (webhookEvent.PaymentSessionId is Guid sessionId && sessionId != Guid.Empty)
        {
            return payment.Sessions.FirstOrDefault(session => session.Id == sessionId);
        }

        if (webhookEvent.GatewayReference is not null)
        {
            var gatewaySession = payment.Sessions.FirstOrDefault(session =>
                session.SessionReference is not null &&
                GatewayReferencesMatch(session.SessionReference, webhookEvent.GatewayReference));

            if (gatewaySession is not null)
            {
                return gatewaySession;
            }
        }

        return payment.Sessions
            .OrderByDescending(session => session.CreatedAtUtc)
            .FirstOrDefault(session => session.IsActive);
    }

    private static bool IsDuplicateOutcome(
        Payment payment,
        PaymentAttempt attempt,
        PaymentSession session,
        GatewayWebhookEvent webhookEvent)
    {
        return webhookEvent.EventType switch
        {
            GatewayWebhookEventType.PaymentSucceeded =>
                payment.Status == PaymentStatus.Captured &&
                attempt.Status == PaymentAttemptStatus.Succeeded &&
                session.Status == PaymentSessionStatus.Completed,
            GatewayWebhookEventType.PaymentFailed =>
                payment.Status == PaymentStatus.Failed &&
                attempt.Status == PaymentAttemptStatus.Failed &&
                session.Status == PaymentSessionStatus.Failed,
            GatewayWebhookEventType.PaymentExpired =>
                payment.Status == PaymentStatus.Expired &&
                attempt.Status == PaymentAttemptStatus.TimedOut &&
                session.Status == PaymentSessionStatus.Expired,
            GatewayWebhookEventType.PaymentCancelled =>
                payment.Status == PaymentStatus.Cancelled &&
                attempt.Status == PaymentAttemptStatus.Cancelled &&
                session.Status == PaymentSessionStatus.Cancelled,
            _ => false
        };
    }

    private static bool GatewayReferencesMatch(
        GatewayReference left,
        GatewayReference right)
    {
        return string.Equals(left.ProviderCode, right.ProviderCode, StringComparison.Ordinal) &&
               string.Equals(left.Reference, right.Reference, StringComparison.Ordinal);
    }

    private static void ApplyWebhookEvent(
        Payment payment,
        PaymentAttempt attempt,
        PaymentSession session,
        GatewayWebhookEvent webhookEvent)
    {
        var gatewayReference = webhookEvent.GatewayReference;
        switch (webhookEvent.EventType)
        {
            case GatewayWebhookEventType.PaymentSucceeded:
                payment.RecordWebhookPaymentSucceeded(
                    attempt,
                    session,
                    new Money(webhookEvent.Amount, webhookEvent.Currency),
                    gatewayReference ?? throw new InvalidOperationException("Successful webhook requires a gateway reference."),
                    webhookEvent.ExternalEventId,
                    webhookEvent.PayloadHash,
                    webhookEvent.OccurredAtUtc);
                return;

            case GatewayWebhookEventType.PaymentFailed:
                payment.RecordWebhookPaymentFailed(
                    attempt,
                    session,
                    webhookEvent.FailureReason,
                    webhookEvent.FailureCode,
                    webhookEvent.FailureMessage,
                    gatewayReference,
                    webhookEvent.ExternalEventId,
                    webhookEvent.PayloadHash,
                    webhookEvent.OccurredAtUtc);
                return;

            case GatewayWebhookEventType.PaymentExpired:
                payment.RecordWebhookPaymentExpired(
                    attempt,
                    session,
                    gatewayReference,
                    webhookEvent.ExternalEventId,
                    webhookEvent.PayloadHash,
                    webhookEvent.OccurredAtUtc);
                return;

            case GatewayWebhookEventType.PaymentCancelled:
                payment.RecordWebhookPaymentCancelled(
                    attempt,
                    session,
                    gatewayReference,
                    webhookEvent.ExternalEventId,
                    webhookEvent.PayloadHash,
                    webhookEvent.OccurredAtUtc);
                return;
        }
    }

    private Task DispatchRestaurantOrderIntegrationAsync(
        Payment payment,
        GatewayWebhookEvent webhookEvent,
        CancellationToken ct)
    {
        return webhookEvent.EventType switch
        {
            GatewayWebhookEventType.PaymentSucceeded => _domainEventDispatcher.DispatchAsync(
                new PaymentCompletedDomainEvent(
                    payment.Id,
                    payment.OrderId,
                    payment.TenantId,
                    payment.BranchId,
                    webhookEvent.Amount,
                    webhookEvent.Currency,
                    payment.ProviderCode,
                    webhookEvent.GatewayReference?.Reference,
                    webhookEvent.OccurredAtUtc),
                ct),
            GatewayWebhookEventType.PaymentCancelled => _domainEventDispatcher.DispatchAsync(
                new PaymentCancelledDomainEvent(
                    payment.Id,
                    payment.OrderId,
                    payment.TenantId,
                    payment.BranchId,
                    payment.RequestedAmount.Amount,
                    payment.RequestedAmount.Currency,
                    payment.ProviderCode,
                    webhookEvent.GatewayReference?.Reference,
                    webhookEvent.OccurredAtUtc),
                ct),
            GatewayWebhookEventType.PaymentFailed or GatewayWebhookEventType.PaymentExpired => _domainEventDispatcher.DispatchAsync(
                new PaymentPendingDomainEvent(
                    payment.Id,
                    payment.OrderId,
                    payment.TenantId,
                    payment.BranchId,
                    payment.RequestedAmount.Amount,
                    payment.RequestedAmount.Currency,
                    payment.ProviderCode,
                    webhookEvent.OccurredAtUtc),
                ct),
            _ => Task.CompletedTask
        };
    }

    private async Task<Payment?> GetExistingPaymentForUpdateAsync(
        PaymentCreationContext context,
        CancellationToken ct)
    {
        var payment = await _unitOfWork.Payments.GetByIdempotencyKeyHashAsync(
            context.TenantId,
            context.IdempotencyKeyHash,
            ct);

        payment ??= await _unitOfWork.Payments.GetByMerchantReferenceAsync(
            context.TenantId,
            context.MerchantReference,
            ct);

        if (payment is null)
        {
            return null;
        }

        await _unitOfWork.Payments.LockAsync(payment.Id, ct);
        payment = await _unitOfWork.Payments.GetAggregateAsync(payment.Id, ct)
            ?? throw new ConflictException("Payment could not be reloaded after lock acquisition.");

        EnsureIdempotentReplay(payment, context);
        return payment;
    }

    private async Task<Payment> ReloadForUpdateAsync(Guid paymentId, CancellationToken ct)
    {
        await _unitOfWork.Payments.LockAsync(paymentId, ct);

        return await _unitOfWork.Payments.GetAggregateAsync(paymentId, ct)
            ?? throw new ConflictException("Payment could not be reloaded after lock acquisition.");
    }

    private static Payment CreateAggregate(PaymentCreationContext context, DateTime now)
    {
        return Payment.Create(
            context.TenantId,
            context.BranchId,
            context.OrderId,
            context.MerchantReference,
            context.Amount,
            context.Method,
            context.ProviderCode,
            context.IdempotencyKeyHash,
            context.RequestFingerprintHash,
            context.CreatedByUserId,
            now,
            context.ExpiresAtUtc);
    }

    private static PaymentIntentionCreateRequest BuildGatewayRequest(
        PaymentCreationContext context,
        Guid paymentId,
        Guid paymentAttemptId)
    {
        return new PaymentIntentionCreateRequest
        {
            PaymentId = paymentId,
            PaymentAttemptId = paymentAttemptId,
            MerchantReference = context.MerchantReference,
            Amount = context.Amount,
            BillingData = context.BillingData,
            Customer = context.Customer,
            Items = context.Items,
            PaymentMethods = context.PaymentMethods,
            NotificationUrl = context.NotificationUrl,
            RedirectionUrl = context.RedirectionUrl,
            ExpirationSeconds = context.ExpirationSeconds,
            CorrelationId = context.CorrelationId
        };
    }

    private static void EnsureIdempotentReplay(Payment payment, PaymentCreationContext context)
    {
        if (!string.Equals(payment.IdempotencyKeyHash, context.IdempotencyKeyHash, StringComparison.Ordinal))
        {
            throw new ConflictException("A payment already exists for this merchant reference.");
        }

        if (!string.Equals(payment.RequestFingerprintHash, context.RequestFingerprintHash, StringComparison.Ordinal))
        {
            throw new ConflictException("The idempotency key was reused with a different payment request.");
        }
    }

    private static void EnsureNoActiveAttempt(Payment payment)
    {
        if (payment.Attempts.Any(static attempt => attempt.IsActive))
        {
            throw new ConflictException("Payment checkout creation is already in progress. Retry later with the same idempotency key.");
        }
    }

    private static PaymentAttempt GetAttempt(Payment payment, Guid paymentAttemptId)
    {
        return payment.Attempts.FirstOrDefault(attempt => attempt.Id == paymentAttemptId)
            ?? throw new ConflictException("Payment attempt could not be reloaded.");
    }

    private IPaymentGateway ResolveGateway(string providerCode)
    {
        var gateway = _gateways.FirstOrDefault(gateway =>
            string.Equals(gateway.ProviderCode, providerCode, StringComparison.Ordinal));

        return gateway ?? throw new ValidationException($"Payment provider '{providerCode}' is not supported.");
    }

    private static CreatePaymentResult? TryCreateDuplicateResult(
        Payment payment,
        PaymentCreationContext context,
        DateTime now)
    {
        var activeSession = payment.Sessions.FirstOrDefault(session => IsSessionReusable(session, now));

        return activeSession is null
            ? null
            : CreateDuplicateResult(payment, activeSession, context);
    }

    private static bool IsSessionReusable(PaymentSession session, DateTime now)
        => session.IsActive && (session.ExpiresAtUtc is null || session.ExpiresAtUtc > now);

    // A hosted checkout link that outlived its provider deadline can no longer
    // produce an outcome, but the provider is not guaranteed to send an expiry
    // webhook. Close the lapsed session and its attempt locally (the Payment
    // aggregate stays non-terminal) so the caller's retry attaches a fresh
    // session to the SAME Payment instead of receiving a dead URL. Attempts
    // without a lapsed session are left alone: they belong to an in-flight
    // creation that EnsureNoActiveAttempt must keep protecting.
    private void ExpireLapsedCheckoutSessions(Payment payment, DateTime now)
    {
        var lapsedSessions = payment.Sessions
            .Where(session => session.IsActive && session.ExpiresAtUtc <= now)
            .ToArray();

        if (lapsedSessions.Length == 0)
        {
            return;
        }

        foreach (var session in lapsedSessions)
        {
            session.Expire(now);
        }

        var activeAttempt = payment.Attempts.FirstOrDefault(static attempt => attempt.IsActive);
        if (activeAttempt is not null)
        {
            payment.RecordAttemptTimedOut(activeAttempt, now, retryAfterUtc: null);
        }

        _logger.LogInformation(
            "Expired lapsed hosted checkout session(s) before retry. PaymentId {PaymentId}. SessionCount {SessionCount}",
            payment.Id,
            lapsedSessions.Length);
    }

    private static CreatePaymentResult CreateResult(
        Payment payment,
        PaymentSession session,
        GatewayPaymentResult gatewayResult,
        bool isDuplicate)
    {
        return new CreatePaymentResult
        {
            PaymentId = payment.Id,
            SessionId = session.Id,
            MerchantReference = payment.MerchantReference.Value,
            ProviderCode = payment.ProviderCode,
            GatewayReference = gatewayResult.GatewayReference.Reference,
            CheckoutUrl = session.CheckoutUrl,
            CheckoutClientSecret = gatewayResult.CheckoutClientSecret,
            Amount = payment.RequestedAmount.Amount,
            Currency = payment.RequestedAmount.Currency,
            PaymentStatus = payment.Status,
            SessionStatus = session.Status,
            ExpiresAtUtc = session.ExpiresAtUtc,
            CorrelationId = gatewayResult.CorrelationId,
            IsDuplicate = isDuplicate
        };
    }

    private static CreatePaymentResult CreateDuplicateResult(
        Payment payment,
        PaymentSession session,
        PaymentCreationContext context)
    {
        return new CreatePaymentResult
        {
            PaymentId = payment.Id,
            SessionId = session.Id,
            MerchantReference = payment.MerchantReference.Value,
            ProviderCode = payment.ProviderCode,
            GatewayReference = session.SessionReference?.Reference ?? string.Empty,
            CheckoutUrl = session.CheckoutUrl,
            CheckoutClientSecret = null,
            Amount = payment.RequestedAmount.Amount,
            Currency = payment.RequestedAmount.Currency,
            PaymentStatus = payment.Status,
            SessionStatus = session.Status,
            ExpiresAtUtc = session.ExpiresAtUtc,
            CorrelationId = context.CorrelationId,
            IsDuplicate = true
        };
    }

    private Exception MapGatewayException(PaymentGatewayException exception)
    {
        if (_environment?.IsDevelopment() == true)
        {
            var diagnosticMessage = $"{exception.Message} CorrelationId: {exception.CorrelationId ?? "none"}.";
            return exception switch
            {
                PaymentGatewayValidationException => new ValidationException(diagnosticMessage),
                _ => new ConflictException(diagnosticMessage)
            };
        }

        return exception switch
        {
            PaymentGatewayValidationException => new ValidationException("Payment provider rejected the checkout request."),
            PaymentGatewayConfigurationException => new ConflictException("Payment provider is not configured."),
            PaymentGatewayAuthenticationException => new ConflictException("Payment provider authentication failed."),
            PaymentGatewayTimeoutException => new ConflictException("Payment provider timed out. Please retry."),
            PaymentGatewayUnavailableException => new ConflictException("Payment provider is temporarily unavailable. Please retry."),
            _ => new ConflictException("Payment provider could not create the checkout session.")
        };
    }

    private static PaymentFailureReason MapFailureReason(PaymentGatewayException exception)
    {
        return exception switch
        {
            PaymentGatewayValidationException => PaymentFailureReason.ValidationFailed,
            PaymentGatewayAuthenticationException => PaymentFailureReason.AuthenticationFailed,
            PaymentGatewayTimeoutException => PaymentFailureReason.Timeout,
            PaymentGatewayUnavailableException unavailable when (int?)unavailable.StatusCode == StatusCodes.Status429TooManyRequests => PaymentFailureReason.RateLimited,
            PaymentGatewayUnavailableException => PaymentFailureReason.ProviderUnavailable,
            _ => PaymentFailureReason.Unknown
        };
    }

    private void LogPreparationRetry(PaymentCreationContext context, int attempt)
    {
        _logger.LogWarning(
            "Retrying payment attempt preparation after concurrency conflict. Attempt {Attempt}. TenantId {TenantId}. MerchantReference {MerchantReference}. CorrelationId {CorrelationId}",
            attempt,
            context.TenantId,
            context.MerchantReference.Value,
            context.CorrelationId);
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException postgres &&
           postgres.SqlState == UniqueViolationSqlState;

    private static bool IsForeignKeyViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException postgres &&
           postgres.SqlState == ForeignKeyViolationSqlState;

    private static PaymentCreationContext CreateContext(CreatePaymentCommand command)
    {
        var merchantReference = new MerchantReference(command.MerchantReference);
        var providerCode = GatewayReference.NormalizeProviderCode(command.ProviderCode);
        var amount = new Money(command.Amount, command.Currency);
        var expiresAtUtc = command.ExpirationSeconds.HasValue
            ? DateTime.UtcNow.AddSeconds(command.ExpirationSeconds.Value)
            : (DateTime?)null;

        return new PaymentCreationContext(
            command.TenantId,
            command.BranchId,
            command.OrderId,
            merchantReference,
            amount,
            command.Method,
            providerCode,
            Hash(command.IdempotencyKey.Trim()),
            Hash(BuildRequestFingerprint(command, merchantReference, providerCode)),
            command.CreatedByUserId,
            expiresAtUtc,
            MapBillingData(command.BillingData),
            MapCustomer(command.Customer),
            MapItems(command.Items, amount.Currency),
            command.PaymentMethods.Select(static method => method.Trim()).Where(static method => method.Length > 0).ToArray(),
            command.NotificationUrl,
            command.RedirectionUrl,
            command.ExpirationSeconds,
            ResolveCorrelationId(command.CorrelationId));
    }

    private static PaymentGatewayBillingData MapBillingData(CreatePaymentBillingData data)
    {
        return new PaymentGatewayBillingData
        {
            FirstName = data.FirstName.Trim(),
            LastName = data.LastName.Trim(),
            Email = data.Email.Trim(),
            PhoneNumber = data.PhoneNumber.Trim(),
            Country = data.Country.Trim().ToUpper(CultureInfo.InvariantCulture),
            City = TrimOptional(data.City),
            State = TrimOptional(data.State),
            PostalCode = TrimOptional(data.PostalCode),
            Street = TrimOptional(data.Street),
            Building = TrimOptional(data.Building),
            Floor = TrimOptional(data.Floor),
            Apartment = TrimOptional(data.Apartment)
        };
    }

    private static PaymentGatewayCustomerData? MapCustomer(CreatePaymentCustomerData? data)
    {
        return data is null
            ? null
            : new PaymentGatewayCustomerData
            {
                FirstName = data.FirstName.Trim(),
                LastName = data.LastName.Trim(),
                Email = data.Email.Trim()
            };
    }

    private static IReadOnlyList<PaymentGatewayLineItem> MapItems(
        IReadOnlyList<CreatePaymentLineItem> items,
        string currency)
    {
        return items.Select(item => new PaymentGatewayLineItem
        {
            Name = item.Name.Trim(),
            Amount = new Money(item.Amount, currency),
            Quantity = item.Quantity,
            Description = TrimOptional(item.Description),
            ImageUrl = TrimOptional(item.ImageUrl)
        }).ToArray();
    }

    private static string BuildRequestFingerprint(
        CreatePaymentCommand command,
        MerchantReference merchantReference,
        string providerCode)
    {
        var builder = new StringBuilder();
        Append(builder, command.TenantId);
        Append(builder, command.BranchId);
        Append(builder, command.OrderId);
        Append(builder, merchantReference.Value);
        Append(builder, command.Amount.ToString("0.00", CultureInfo.InvariantCulture));
        Append(builder, command.Currency.Trim().ToUpper(CultureInfo.InvariantCulture));
        Append(builder, command.Method);
        Append(builder, providerCode);
        Append(builder, command.ExpirationSeconds);
        Append(builder, command.NotificationUrl);
        Append(builder, command.RedirectionUrl);
        AppendBilling(builder, command.BillingData);
        AppendCustomer(builder, command.Customer);
        AppendCollection(builder, command.PaymentMethods);
        AppendItems(builder, command.Items);

        return builder.ToString();
    }

    private static void AppendBilling(StringBuilder builder, CreatePaymentBillingData data)
    {
        Append(builder, data.FirstName);
        Append(builder, data.LastName);
        Append(builder, data.Email);
        Append(builder, data.PhoneNumber);
        Append(builder, data.Country);
        Append(builder, data.City);
        Append(builder, data.State);
        Append(builder, data.PostalCode);
        Append(builder, data.Street);
        Append(builder, data.Building);
        Append(builder, data.Floor);
        Append(builder, data.Apartment);
    }

    private static void AppendCustomer(StringBuilder builder, CreatePaymentCustomerData? data)
    {
        if (data is null)
        {
            Append(builder, "<null>");
            return;
        }

        Append(builder, data.FirstName);
        Append(builder, data.LastName);
        Append(builder, data.Email);
    }

    private static void AppendItems(StringBuilder builder, IReadOnlyList<CreatePaymentLineItem> items)
    {
        foreach (var item in items)
        {
            Append(builder, item.Name);
            Append(builder, item.Amount.ToString("0.00", CultureInfo.InvariantCulture));
            Append(builder, item.Quantity);
            Append(builder, item.Description);
            Append(builder, item.ImageUrl);
        }
    }

    private static void AppendCollection(StringBuilder builder, IReadOnlyList<string> values)
    {
        foreach (var value in values.Select(static value => value.Trim()).Order(StringComparer.Ordinal))
        {
            Append(builder, value);
        }
    }

    private static void Append(StringBuilder builder, object? value)
    {
        builder.Append(value?.ToString()?.Trim() ?? string.Empty);
        builder.Append('|');
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLower(CultureInfo.InvariantCulture);
    }

    private static string ResolveCorrelationId(string? correlationId)
        => string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)
            : correlationId.Trim();

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record PreparedPaymentAttempt(
        Guid PaymentId,
        Guid PaymentAttemptId,
        CreatePaymentResult? DuplicateResult)
    {
        public static PreparedPaymentAttempt Pending(Guid paymentId, Guid paymentAttemptId)
            => new(paymentId, paymentAttemptId, null);

        public static PreparedPaymentAttempt Duplicate(CreatePaymentResult result)
            => new(Guid.Empty, Guid.Empty, result);
    }

    private sealed record PaymentCreationContext(
        Guid TenantId,
        Guid BranchId,
        Guid OrderId,
        MerchantReference MerchantReference,
        Money Amount,
        PaymentMethod Method,
        string ProviderCode,
        string IdempotencyKeyHash,
        string RequestFingerprintHash,
        Guid? CreatedByUserId,
        DateTime? ExpiresAtUtc,
        PaymentGatewayBillingData BillingData,
        PaymentGatewayCustomerData? Customer,
        IReadOnlyList<PaymentGatewayLineItem> Items,
        IReadOnlyList<string> PaymentMethods,
        string? NotificationUrl,
        string? RedirectionUrl,
        int? ExpirationSeconds,
        string CorrelationId);
}
