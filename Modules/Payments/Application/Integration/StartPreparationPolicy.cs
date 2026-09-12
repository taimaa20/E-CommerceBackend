namespace RestaurantPos.Api.Modules.Payments.Application.Integration;

public enum StartPreparationPolicy
{
    Immediately = 0,
    AfterPayment = 1,
    ManualApproval = 2
}
