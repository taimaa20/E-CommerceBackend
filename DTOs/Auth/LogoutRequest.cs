using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs.Auth
{
    public class LogoutRequest
    {
        [Required]
        [MaxLength(512)]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
