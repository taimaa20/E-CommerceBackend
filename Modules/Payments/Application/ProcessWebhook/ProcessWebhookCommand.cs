using MediatR;

namespace RestaurantPos.Api.Modules.Payments.Application.ProcessWebhook;

public sealed record ProcessWebhookCommand : IRequest<ProcessWebhookResult>
{
    public required string ProviderCode { get; init; }

    public required string RawBody { get; init; }

    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Query { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public required string CorrelationId { get; init; }

    public string? RemoteIpAddress { get; init; }
}
