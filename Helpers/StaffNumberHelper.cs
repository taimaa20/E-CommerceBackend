namespace RestaurantPos.Api.Helpers
{
    public static class StaffNumberHelper
    {
        public static string BuildGeneratedStaffNumber(string? username, Guid userId)
        {
            var seed = string.IsNullOrWhiteSpace(username)
                ? "STAFF"
                : new string(username.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(seed))
            {
                seed = "STAFF";
            }

            var prefix = seed.Length <= 10 ? seed : seed[..10];
            var suffix = userId.ToString("N")[..6].ToUpperInvariant();
            var staffNo = $"{prefix}-{suffix}";
            return staffNo.Length <= 20 ? staffNo : staffNo[..20];
        }
    }
}
