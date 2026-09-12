using Microsoft.Extensions.Options;

namespace RestaurantPos.Api.Modules.Payments.Application.Integration;

public sealed class PaymentRestaurantPosOptions
{
    public const string SectionName = "Payments:RestaurantPosIntegration";

    public StartPreparationPolicy StartPreparationPolicy { get; set; } = StartPreparationPolicy.AfterPayment;
}

public sealed class PaymentRestaurantPosOptionsValidator : IValidateOptions<PaymentRestaurantPosOptions>
{
    public ValidateOptionsResult Validate(string? name, PaymentRestaurantPosOptions options)
    {
        return Enum.IsDefined(options.StartPreparationPolicy)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{PaymentRestaurantPosOptions.SectionName}:StartPreparationPolicy is invalid.");
    }
}
