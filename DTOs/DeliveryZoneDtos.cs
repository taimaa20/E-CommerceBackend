using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs
{
    public class DeliveryZoneDto
    {
        public Guid Id { get; set; }
        public string NameEn { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string Code { get; set; } = string.Empty;
        public decimal DeliveryFee { get; set; }
        public decimal DeliveryCost { get; set; }
        public DeliveryPaymentMode PaymentMode { get; set; }
        public decimal? CenterLatitude { get; set; }
        public decimal? CenterLongitude { get; set; }
        public int? RadiusMeters { get; set; }
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class DeliveryZoneSelectDto
    {
        public Guid Id { get; set; }
        public string NameEn { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string Code { get; set; } = string.Empty;
        public decimal DeliveryFee { get; set; }
        public decimal DeliveryCost { get; set; }
        public DeliveryPaymentMode PaymentMode { get; set; }
        public decimal? CenterLatitude { get; set; }
        public decimal? CenterLongitude { get; set; }
        public int? RadiusMeters { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class DeliveryZoneCreateDto
    {
        [Required]
        [MaxLength(120)]
        public string NameEn { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? NameAr { get; set; }

        [Required]
        [MaxLength(40)]
        public string Code { get; set; } = string.Empty;

        [Range(0, 9999999999999999.99)]
        public decimal DeliveryFee { get; set; }

        [Range(0, 9999999999999999.99)]
        public decimal DeliveryCost { get; set; }

        [EnumDataType(typeof(DeliveryPaymentMode))]
        public DeliveryPaymentMode PaymentMode { get; set; } = DeliveryPaymentMode.CustomerPays;

        [Range(-90, 90)]
        public decimal? CenterLatitude { get; set; }

        [Range(-180, 180)]
        public decimal? CenterLongitude { get; set; }

        [Range(1, int.MaxValue)]
        public int? RadiusMeters { get; set; }

        public bool IsActive { get; set; } = true;

        [Range(0, int.MaxValue)]
        public int DisplayOrder { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    public class DeliveryZoneUpdateDto
    {
        [Required]
        [MaxLength(120)]
        public string NameEn { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? NameAr { get; set; }

        [Required]
        [MaxLength(40)]
        public string Code { get; set; } = string.Empty;

        [Range(0, 9999999999999999.99)]
        public decimal DeliveryFee { get; set; }

        [Range(0, 9999999999999999.99)]
        public decimal DeliveryCost { get; set; }

        [EnumDataType(typeof(DeliveryPaymentMode))]
        public DeliveryPaymentMode PaymentMode { get; set; } = DeliveryPaymentMode.CustomerPays;

        [Range(-90, 90)]
        public decimal? CenterLatitude { get; set; }

        [Range(-180, 180)]
        public decimal? CenterLongitude { get; set; }

        [Range(1, int.MaxValue)]
        public int? RadiusMeters { get; set; }

        public bool IsActive { get; set; } = true;

        [Range(0, int.MaxValue)]
        public int DisplayOrder { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    public class DeliveryZonePagedResultDto
    {
        public IReadOnlyList<DeliveryZoneDto> Items { get; set; } = new List<DeliveryZoneDto>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}
