using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Helpers
{
    public static class UserRoleExtensions
    {
        /// <summary>
        /// SuperAdmin is a strict superset of Admin. Any capability gated on
        /// "is this user an Admin" (when the role is read from the persisted
        /// <see cref="User"/> entity rather than the auth claim) must also admit
        /// SuperAdmin. Claim-based checks already treat SuperAdmin as Admin via
        /// the primary role claim emitted by TokenService, so this is only for
        /// in-memory checks against a loaded entity's <see cref="UserRole"/>.
        /// EF Core query filters must inline the same comparison — a method call
        /// cannot be translated to SQL.
        /// </summary>
        public static bool IsAdminOrAbove(this UserRole role)
            => role is UserRole.Admin or UserRole.SuperAdmin;
    }
}
