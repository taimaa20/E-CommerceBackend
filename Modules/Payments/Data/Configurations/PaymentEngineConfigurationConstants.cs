namespace RestaurantPos.Api.Modules.Payments.Data.Configurations;

/// <summary>
/// Persistence constants for the Payment Engine module.
/// Keeping table names clean while using a schema prevents collision with the legacy POS Payments table.
/// </summary>
internal static class PaymentEngineConfigurationConstants
{
    public const string Schema = "PaymentEngine";

    public const string RowVersionProperty = "RowVersion";

    public const int ProviderCodeMaxLength = 60;
    public const int HashMaxLength = 128;
    public const int ExternalReferenceMaxLength = 160;
    public const int FailureCodeMaxLength = 80;
    public const int FailureMessageMaxLength = 500;
}
