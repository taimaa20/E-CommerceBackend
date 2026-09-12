namespace RestaurantPos.Api.DTOs.Auth
{
    public class AuthUserDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? FullNameAr { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Role { get; set; }
        public string? EmployeeNumber { get; set; }
        public string? Image { get; set; }
        public string? ImageUrl { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("employee_number")]
        public string? EmployeeNumberSnake => EmployeeNumber;
    }
}
