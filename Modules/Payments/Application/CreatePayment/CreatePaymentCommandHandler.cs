using MediatR;

namespace RestaurantPos.Api.Modules.Payments.Application.CreatePayment;

public sealed class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, CreatePaymentResult>
{
    private readonly CreatePaymentCommandValidator _validator;
    private readonly IPaymentOrchestrator _orchestrator;

    public CreatePaymentCommandHandler(
        CreatePaymentCommandValidator validator,
        IPaymentOrchestrator orchestrator)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    public Task<CreatePaymentResult> Handle(CreatePaymentCommand command, CancellationToken ct)
    {
        _validator.ValidateAndThrow(command);
        return _orchestrator.CreatePaymentAsync(command, ct);
    }
}
