using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Helpers
{
    public static class CashierShiftUserEligibility
    {
        public static bool CanOwnShift(UserRole role)
            => role.IsAdminOrAbove() || role is UserRole.Cashier;
    }
}
