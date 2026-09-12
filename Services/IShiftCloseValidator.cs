using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    /// <summary>
    /// Evaluates a shift close attempt against the tenant's ShiftRulesConfig.
    /// Pure read — does not mutate the shift or the database.
    /// </summary>
    public interface IShiftCloseValidator
    {
        Task<ShiftCloseValidationDto> ValidateAsync(
            Guid tenantId,
            CashierBalanceShift shift,
            DateTime windowEndUtc,
            CancellationToken ct);
    }
}
