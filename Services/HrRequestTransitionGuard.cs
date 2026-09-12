using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    /// Single transition policy for leave / loan / permission status changes.
    /// - Only `pending` is mutable.
    /// - Admin/manager may set approved | rejected | cancelled.
    /// - The original requester may only cancel (and only their own pending request).
    /// Throws InvalidOperationException with a stable, user-facing message on violation.
    public static class HrRequestTransitionGuard
    {
        public static void Apply(
            string currentStatus,
            ref string newStatus,
            Guid createdByUserId,
            Guid actingUserId,
            bool isAdmin)
        {
            if (string.IsNullOrWhiteSpace(newStatus) || !MobileRequestStatuses.All.Contains(newStatus))
                throw new ArgumentException("Invalid status.", nameof(newStatus));

            if (currentStatus != MobileRequestStatuses.Pending)
                throw new InvalidOperationException(
                    $"Cannot change status from '{currentStatus}'. Only pending requests are mutable.");

            if (!MobileRequestStatuses.AllowedTransitions[currentStatus].Contains(newStatus))
                throw new InvalidOperationException(
                    $"Cannot transition from '{currentStatus}' to '{newStatus}'.");

            if (!isAdmin)
            {
                if (newStatus != MobileRequestStatuses.Cancelled)
                    throw new UnauthorizedAccessException("Only an administrator may approve or reject this request.");
                if (createdByUserId != actingUserId)
                    throw new UnauthorizedAccessException("Only the requester may cancel their own request.");
            }
        }
    }
}
