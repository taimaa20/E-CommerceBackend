namespace RestaurantPos.Api.Models
{
    public class UserTokenPayload
    {
        public Guid UserId { get; set; }
        public Guid TenantId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? DeviceId { get; set; }
        public string? FullName { get; set; }
        public string? FullNameAr { get; set; }
    }
}
