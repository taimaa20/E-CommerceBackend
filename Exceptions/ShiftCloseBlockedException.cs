using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Exceptions
{
    /// <summary>
    /// Thrown when CashierShiftService.CloseShiftAsync is blocked by the
    /// validation engine and the request did not include a valid Force flag.
    /// Carries the full ShiftCloseValidationDto so the controller can return it
    /// to the frontend's close-shift confirmation popup.
    /// Mapped to HTTP 409 by ExceptionMiddleware.
    /// </summary>
    public class ShiftCloseBlockedException : ConflictException
    {
        public ShiftCloseValidationDto Validation { get; }

        public ShiftCloseBlockedException(ShiftCloseValidationDto validation)
            : base("Shift close blocked by open orders.")
        {
            Validation = validation;
        }
    }
}
