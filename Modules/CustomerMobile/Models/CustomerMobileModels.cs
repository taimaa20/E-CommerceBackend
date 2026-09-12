using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.Models
{
    public enum CustomerPreferredLanguage
    {
        En = 0,
        Ar = 1
    }

    public enum CustomerDeviceType
    {
        Unknown = 0,
        Android = 1,
        Ios = 2,
        Web = 3
    }

    public enum CustomerMobileOrderType
    {
        Pickup = 0,
        Delivery = 1
    }

    public enum CustomerMobileAuditAction
    {
        AccountDeleted = 0,
        OrderCancelled = 1,
        NotificationRead = 2,
        LoyaltyCalculated = 3
    }

    public enum CustomerGender
    {
        Unspecified = 0,
        Male = 1,
        Female = 2
    }

    public static class CustomerOtpPurposes
    {
        public const string PhoneLogin = "phone-login";
        public const string VerifyAccount = "verify-account";
        public const string ResetPassword = "reset-password";
    }

    public class CustomerAccount : BaseEntity
    {
        [Required]
        [MaxLength(32)]
        public string CustomerNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string MobileNumber { get; set; } = string.Empty;

        // Canonical phone number used by the OTP login flow. Stored separately from
        // MobileNumber so legacy email/password customers retain their existing
        // MobileNumber value and we can enforce a unique index on the normalized phone.
        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? PasswordSalt { get; set; }

        public bool IsVerified { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; }
        public bool IsProfileCompleted { get; set; }
        public CustomerPreferredLanguage PreferredLanguage { get; set; } = CustomerPreferredLanguage.En;
        public DateTime? LastLoginDate { get; set; }
        public DateTime? BirthDate { get; set; }
        public CustomerGender Gender { get; set; } = CustomerGender.Unspecified;

        public ICollection<CustomerAddress> Addresses { get; set; } = new List<CustomerAddress>();
        public ICollection<CustomerDevice> Devices { get; set; } = new List<CustomerDevice>();
        public ICollection<CustomerRefreshToken> RefreshTokens { get; set; } = new List<CustomerRefreshToken>();
        public ICollection<CustomerCart> Carts { get; set; } = new List<CustomerCart>();
        public ICollection<CustomerOtp> Otps { get; set; } = new List<CustomerOtp>();
    }

    public class CustomerOtp : BaseEntity
    {
        public Guid CustomerId { get; set; }
        public CustomerAccount Customer { get; set; } = null!;

        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string OtpCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(40)]
        public string Purpose { get; set; } = CustomerOtpPurposes.PhoneLogin;

        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }
    }

    public class CustomerAddress : BaseEntity
    {
        public Guid CustomerId { get; set; }
        public CustomerAccount Customer { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string AddressName { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string Area { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Street { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Building { get; set; }

        [MaxLength(50)]
        public string? Floor { get; set; }

        [MaxLength(50)]
        public string? Apartment { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal? Latitude { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal? Longitude { get; set; }

        public Guid? DeliveryZoneId { get; set; }
        public DeliveryZone? DeliveryZone { get; set; }
        public bool IsDefault { get; set; }
    }

    public class CustomerDevice : BaseEntity
    {
        public Guid CustomerId { get; set; }
        public CustomerAccount Customer { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string DeviceId { get; set; } = string.Empty;

        public CustomerDeviceType DeviceType { get; set; } = CustomerDeviceType.Unknown;

        [MaxLength(500)]
        public string? FcmToken { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    }

    public class CustomerRefreshToken : BaseEntity
    {
        public Guid CustomerId { get; set; }
        public CustomerAccount Customer { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string TokenHash { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? DeviceId { get; set; }

        [MaxLength(200)]
        public string? DeviceName { get; set; }

        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }

        [MaxLength(200)]
        public string? ReplacedByTokenHash { get; set; }

        [NotMapped]
        public bool IsActive => RevokedAt == null && ExpiresAt > DateTime.UtcNow;
    }

    public class CustomerCart : BaseEntity
    {
        public Guid CustomerId { get; set; }
        public CustomerAccount Customer { get; set; } = null!;

        public bool IsActive { get; set; } = true;
        public ICollection<CustomerCartItem> Items { get; set; } = new List<CustomerCartItem>();
    }

    public class CustomerCartItem : BaseEntity
    {
        public Guid CartId { get; set; }
        public CustomerCart Cart { get; set; } = null!;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public int Quantity { get; set; }

        public Guid? SelectedOptionId { get; set; }
        public ProductOption? SelectedOption { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public ICollection<CustomerCartItemModifier> Modifiers { get; set; } = new List<CustomerCartItemModifier>();
    }

    public class CustomerCartItemModifier : BaseEntity
    {
        public Guid CartItemId { get; set; }
        public CustomerCartItem CartItem { get; set; } = null!;

        public Guid ModifierId { get; set; }
        public Modifier Modifier { get; set; } = null!;

        public int Quantity { get; set; } = 1;
    }

    public class CustomerMobileOrder : BaseEntity
    {
        public Guid CustomerId { get; set; }
        public CustomerAccount Customer { get; set; } = null!;

        public Guid OrderId { get; set; }
        public Order Order { get; set; } = null!;

        [Required]
        [MaxLength(64)]
        // User-facing configured number snapshot. Order.OrderNumber remains the
        // unique internal system reference.
        public string OrderNumber { get; set; } = string.Empty;

        public CustomerMobileOrderType OrderType { get; set; }
    }

    public class CustomerMobileAuditLog : BaseEntity
    {
        public Guid CustomerId { get; set; }
        public CustomerAccount Customer { get; set; } = null!;

        public CustomerMobileAuditAction Action { get; set; }

        public Guid? OrderId { get; set; }

        public Guid? NotificationId { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        [MaxLength(1000)]
        public string? Metadata { get; set; }

        public DateTime ActionAt { get; set; } = DateTime.UtcNow;
    }
}
