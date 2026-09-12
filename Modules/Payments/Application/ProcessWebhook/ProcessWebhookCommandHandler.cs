using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MediatR;
using RestaurantPos.Api.Modules.Payments.Application.CreatePayment;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;
using RestaurantPos.Api.Modules.Payments.Infrastructure.Gateways;

namespace RestaurantPos.Api.Modules.Payments.Application.ProcessWebhook;

public sealed class ProcessWebhookCommandHandler : IRequestHandler<ProcessWebhookCommand, ProcessWebhookResult>
{
    private readonly IReadOnlyCollection<IPaymentWebhookVerifier> _verifiers;
    private readonly IReadOnlyCollection<IPaymentWebhookMapper> _mappers;
    private readonly IPaymentOrchestrator _orchestrator;
    private readonly ILogger<ProcessWebhookCommandHandler> _logger;

    public ProcessWebhookCommandHandler(
        IEnumerable<IPaymentWebhookVerifier> verifiers,
        IEnumerable<IPaymentWebhookMapper> mappers,
        IPaymentOrchestrator orchestrator,
        ILogger<ProcessWebhookCommandHandler> logger)
    {
        _verifiers = verifiers?.ToArray() ?? throw new ArgumentNullException(nameof(verifiers));
        _mappers = mappers?.ToArray() ?? throw new ArgumentNullException(nameof(mappers));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProcessWebhookResult> Handle(ProcessWebhookCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var providerCode = GatewayReference.NormalizeProviderCode(command.ProviderCode);
        var verifier = Resolve(_verifiers, providerCode);
        var mapper = Resolve(_mappers, providerCode);
        if (verifier is null || mapper is null)
        {
            return ProcessWebhookResult.Rejected(
                StatusCodes.Status400BadRequest,
                command.CorrelationId,
                "Payment webhook provider is not supported.");
        }

        var verification = await verifier.VerifyAsync(
            new PaymentWebhookVerificationRequest
            {
                RawBody = command.RawBody,
                Headers = command.Headers,
                Query = command.Query,
                CorrelationId = command.CorrelationId,
                RemoteIpAddress = command.RemoteIpAddress
            },
            ct);

        if (!verification.IsValid)
        {
            return ProcessWebhookResult.Rejected(
                MapVerificationStatusCode(verification.Reason),
                command.CorrelationId,
                verification.Message);
        }

        try
        {
            var webhookEvent = mapper.Map(
                command.RawBody,
                HashPayload(command.RawBody),
                command.CorrelationId);

            return await _orchestrator.ProcessWebhookAsync(webhookEvent, ct);
        }
        catch (PaymentWebhookMappingException ex)
        {
            _logger.LogWarning(
                "Rejected payment webhook payload after verification. ProviderCode {ProviderCode}. CorrelationId {CorrelationId}. Reason {Reason}",
                providerCode,
                command.CorrelationId,
                ex.Message);

            return ProcessWebhookResult.Rejected(
                StatusCodes.Status400BadRequest,
                command.CorrelationId,
                "Payment webhook payload is not supported.");
        }
    }

    private static T? Resolve<T>(IReadOnlyCollection<T> values, string providerCode)
        where T : class
    {
        return values.FirstOrDefault(value =>
        {
            var code = value switch
            {
                IPaymentWebhookVerifier verifier => verifier.ProviderCode,
                IPaymentWebhookMapper mapper => mapper.ProviderCode,
                _ => string.Empty
            };

            return string.Equals(code, providerCode, StringComparison.Ordinal);
        });
    }

    private static int MapVerificationStatusCode(PaymentWebhookRejectionReason reason)
    {
        return reason switch
        {
            PaymentWebhookRejectionReason.ProviderDisabled => StatusCodes.Status503ServiceUnavailable,
            PaymentWebhookRejectionReason.ProviderMisconfigured => StatusCodes.Status503ServiceUnavailable,
            PaymentWebhookRejectionReason.SignatureMissing => StatusCodes.Status401Unauthorized,
            PaymentWebhookRejectionReason.SignatureInvalid => StatusCodes.Status401Unauthorized,
            PaymentWebhookRejectionReason.ReplayWindowExceeded => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status400BadRequest
        };
    }

    private static string HashPayload(string rawBody)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawBody));
        return Convert.ToHexString(bytes).ToLower(CultureInfo.InvariantCulture);
    }
}
