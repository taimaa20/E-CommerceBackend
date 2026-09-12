using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs.Auth
{
    public class RefreshTokenRequest
    {
        [Required]
        [MaxLength(512)]
        public string RefreshToken { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? DeviceId { get; set; }
    }
}
