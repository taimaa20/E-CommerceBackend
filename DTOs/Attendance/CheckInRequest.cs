using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs.Attendance
{
    public class CheckInRequest
    {
        [MaxLength(500)]
        public string? Note { get; set; }

        [Range(-90, 90)]
        public decimal? Latitude { get; set; }

        [Range(-180, 180)]
        public decimal? Longitude { get; set; }
    }
}
