using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs.Auth
{
    public class LoginRequest
    {
        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string Password { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? DeviceId { get; set; }

        [MaxLength(200)]
        public string? DeviceName { get; set; }
    }
}
