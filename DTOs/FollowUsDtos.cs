using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs
{
    public class FollowUsClickCreateDto
    {
        [Required, MaxLength(50)]
        public string Platform { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string Url { get; set; } = string.Empty;
    }
}
