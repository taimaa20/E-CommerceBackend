using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Modules.CustomerMobile.DTOs
{
    public class CustomerMobileBranchDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public bool IsMainBranch { get; set; }
    }

    public class CustomerRegisterRequest
    {
        [Required, StringLength(100, MinimumLength = 1)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 1)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(20, MinimumLength = 3), Phone]
        public string MobileNumber { get; set; } = string.Empty;

        [Required, StringLength(128, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;

        [StringLength(5)]
        public string? PreferredLanguage { get; set; }
    }

    public class CustomerLoginRequest
    {
        [Required, StringLength(150)]
        public string Identifier { get; set; } = string.Empty;

        [Required, StringLength(128, MinimumLength = 1)]
        public string Password { get; set; } = string.Empty;

        [StringLength(200)]
        public string? DeviceId { get; set; }

        [StringLength(200)]
        public string? DeviceName { get; set; }

        [StringLength(20)]
        public string? DeviceType { get; set; }

        [StringLength(500)]
        public string? FcmToken { get; set; }
    }

    public class CustomerRefreshTokenRequest
    {
        [Required, StringLength(512)]
        public string RefreshToken { get; set; } = string.Empty;

        [StringLength(200)]
        public string? DeviceId { get; set; }
    }

    public class CustomerLogoutRequest
    {
        [StringLength(512)]
        public string? RefreshToken { get; set; }
    }

    public class CustomerForgotPasswordRequest
    {
        [Required, StringLength(150)]
        public string Identifier { get; set; } = string.Empty;
    }

    public class CustomerResetPasswordRequest
    {
        [Required, StringLength(150)]
        public string Identifier { get; set; } = string.Empty;

        [Required, StringLength(20)]
        public string OtpCode { get; set; } = string.Empty;

        [Required, StringLength(128, MinimumLength = 8)]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class CustomerVerifyOtpRequest
    {
        [Required, StringLength(150)]
        public string Identifier { get; set; } = string.Empty;

        [Required, StringLength(20)]
        public string OtpCode { get; set; } = string.Empty;
    }

    /// <summary>Single-entry auth: looks up by phone, creates the customer if unknown,
    /// then sends an OTP. No password, no register/login split.</summary>
    public class CustomerStartAuthRequest
    {
        [Required, StringLength(20, MinimumLength = 6), Phone]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(200)]
        public string? DeviceId { get; set; }

        [StringLength(200)]
        public string? DeviceName { get; set; }
    }

    public class CustomerStartAuthResponse
    {
        public bool Success { get; set; } = true;
        public string PhoneNumber { get; set; } = string.Empty;
        public bool OtpSent { get; set; } = true;
        public bool IsExistingCustomer { get; set; }
        public bool IsPhoneVerified { get; set; }
        public bool IsProfileCompleted { get; set; }
    }

    public class CustomerVerifyAuthRequest
    {
        [Required, StringLength(20, MinimumLength = 6), Phone]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required, StringLength(10, MinimumLength = 4)]
        public string Otp { get; set; } = string.Empty;

        [StringLength(200)]
        public string? DeviceId { get; set; }

        [StringLength(200)]
        public string? DeviceName { get; set; }

        [StringLength(20)]
        public string? DeviceType { get; set; }

        [StringLength(500)]
        public string? FcmToken { get; set; }
    }

    public class CustomerVerifyAuthResponse
    {
        public string Token { get; set; } = string.Empty;

        // Access-token lifetime in seconds, per the mobile-auth spec. The
        // refresh token lives on a separate channel and is returned for
        // long-lived sessions; mobile clients can ignore it if they only
        // need a single JWT.
        public int ExpiresIn { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime RefreshTokenExpiresAt { get; set; }

        public CustomerSessionDto Customer { get; set; } = new();

        // "COMPLETE_PROFILE" or "HOME" — drives the post-login mobile screen.
        public string NextAction { get; set; } = CustomerNextActions.Home;
    }

    public class CustomerSessionDto
    {
        public Guid Id { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public bool IsPhoneVerified { get; set; }
        public bool IsProfileCompleted { get; set; }
    }

    public class CustomerMeDto
    {
        public Guid Id { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime? BirthDate { get; set; }
        public bool IsPhoneVerified { get; set; }
        public bool IsProfileCompleted { get; set; }
    }

    public class CustomerCompleteProfileRequest
    {
        [Required, StringLength(100, MinimumLength = 1)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 1)]
        public string LastName { get; set; } = string.Empty;

        [EmailAddress, StringLength(150)]
        public string? Email { get; set; }

        public DateTime? BirthDate { get; set; }
    }

    public class CustomerCompleteProfileResponse
    {
        public bool Success { get; set; } = true;
        public bool IsPhoneVerified { get; set; }
        public bool IsProfileCompleted { get; set; }
        public string NextAction { get; set; } = CustomerNextActions.Home;
    }

    public static class CustomerNextActions
    {
        public const string CompleteProfile = "COMPLETE_PROFILE";
        public const string Home = "HOME";
    }

    public class CustomerLoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime AccessTokenExpiresAt { get; set; }
        public DateTime RefreshTokenExpiresAt { get; set; }
        public CustomerProfileDto Customer { get; set; } = new();
    }

    public class CustomerProfileDto
    {
        public Guid Id { get; set; }
        public string CustomerNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string PreferredLanguage { get; set; } = "en";
        public bool IsVerified { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginDate { get; set; }
    }

    public class CustomerProfileUpdateRequest
    {
        [Required, StringLength(100, MinimumLength = 1)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 1)]
        public string LastName { get; set; } = string.Empty;

        [StringLength(5)]
        public string? PreferredLanguage { get; set; }
    }

    public class CustomerAddressDto
    {
        public Guid Id { get; set; }
        public string AddressName { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string? Building { get; set; }
        public string? Floor { get; set; }
        public string? Apartment { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public Guid? DeliveryZoneId { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CustomerAddressRequest
    {
        [Required, StringLength(100, MinimumLength = 1)]
        public string AddressName { get; set; } = string.Empty;

        [Required, StringLength(120, MinimumLength = 1)]
        public string Area { get; set; } = string.Empty;

        [Required, StringLength(200, MinimumLength = 1)]
        public string Street { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Building { get; set; }

        [StringLength(50)]
        public string? Floor { get; set; }

        [StringLength(50)]
        public string? Apartment { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public Guid? DeliveryZoneId { get; set; }
        public bool IsDefault { get; set; }
    }

    public class CustomerCartDto
    {
        public Guid Id { get; set; }
        public List<CustomerCartItemDto> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
    }

    public class CustomerCartItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductNameAr { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public Guid? SelectedOptionId { get; set; }
        public string? SelectedOptionName { get; set; }
        public string? SelectedOptionNameAr { get; set; }
        public string? Notes { get; set; }
        public List<CustomerCartItemModifierDto> Modifiers { get; set; } = new();
    }

    public class CustomerCartItemModifierDto
    {
        public Guid ModifierId { get; set; }
        public string ModifierName { get; set; } = string.Empty;
        public string? ModifierNameAr { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }

    public class CustomerCartItemRequest
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public Guid? SelectedOptionId { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public List<CustomerCartItemModifierRequest> Modifiers { get; set; } = new();
    }

    public class CustomerCartItemUpdateRequest
    {
        public int Quantity { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public List<CustomerCartItemModifierRequest> Modifiers { get; set; } = new();
    }

    public class CustomerCartItemModifierRequest
    {
        public Guid ModifierId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class CustomerOrderCreateRequest
    {
        [Required, StringLength(20)]
        public string OrderType { get; set; } = "Pickup";

        public Guid? DeliveryAddressId { get; set; }

        [Required, StringLength(50)]
        public string PaymentMethod { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Notes { get; set; }

        public List<CustomerOrderItemRequest> Items { get; set; } = new();
    }

    public class CustomerOrderItemRequest
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public Guid? SelectedOptionId { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public List<CustomerCartItemModifierRequest> Modifiers { get; set; } = new();
    }

    public class CustomerOrderCreateResponse
    {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class CustomerCancelOrderRequest
    {
        [Required, StringLength(500, MinimumLength = 3)]
        public string Reason { get; set; } = string.Empty;
    }

    public class CustomerCancelOrderResponse
    {
        public bool Success { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class CustomerOrderListItemDto
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int TotalItemsCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CustomerOrderDetailsDto : CustomerOrderListItemDto
    {
        public string? DeliveryAddress { get; set; }
        public string? DeliveryNotes { get; set; }
        public List<CustomerOrderItemDto> Items { get; set; } = new();
    }

    public class CustomerOrderItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductNameEn { get; set; } = string.Empty;
        public string? ProductNameAr { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public string? Notes { get; set; }
        public string? ImageUrl { get; set; }
        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryNameAr { get; set; }
        public Guid? SelectedOptionId { get; set; }
        public string? SelectedOptionName { get; set; }
        public string? SelectedOptionNameAr { get; set; }
        public Guid? VariantId { get; set; }
        public string? VariantName { get; set; }
        public string? VariantNameAr { get; set; }
        public List<CustomerOrderItemModifierDto> Modifiers { get; set; } = new();
    }

    public class CustomerOrderItemModifierDto
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }

    public class CustomerHomeDto
    {
        public List<CustomerHomeBannerDto> Banners { get; set; } = new();
        public List<PublicCategoryDto> Categories { get; set; } = new();
        public List<PublicProductDto> PopularProducts { get; set; } = new();
        public List<PublicProductDto> FeaturedProducts { get; set; } = new();
        public List<OfferResponseDto> AvailableOffers { get; set; } = new();
        public List<CustomerOrderListItemDto> LastOrders { get; set; } = new();
    }

    public class CustomerHomeBannerDto
    {
        public string? Title { get; set; }
        public string? TitleAr { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class CustomerSearchResponseDto
    {
        public List<PublicProductDto> Products { get; set; } = new();
        public List<PublicCategoryDto> Categories { get; set; } = new();
        public List<OfferResponseDto> Offers { get; set; } = new();
    }

    public class CustomerDeliveryCheckRequest
    {
        [Range(-90, 90)]
        public decimal Latitude { get; set; }

        [Range(-180, 180)]
        public decimal Longitude { get; set; }
    }

    public class CustomerDeliveryZoneDto
    {
        public Guid Id { get; set; }
        public string NameEn { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string Code { get; set; } = string.Empty;
        public decimal DeliveryFee { get; set; }
        public string PaymentMode { get; set; } = string.Empty;
    }

    public class CustomerDeliveryCheckResponse
    {
        public bool IsAvailable { get; set; }
        public CustomerDeliveryZoneDto? Zone { get; set; }
        public decimal DeliveryFee { get; set; }
        public string? Message { get; set; }
    }

    public class CustomerOrderCalculateRequest
    {
        [Required, StringLength(20)]
        public string OrderType { get; set; } = "Pickup";

        public Guid? DeliveryAddressId { get; set; }

        public List<CustomerOrderItemRequest> Items { get; set; } = new();
    }

    public class CustomerOrderCalculationResponse
    {
        public decimal Subtotal { get; set; }
        public decimal Discounts { get; set; }
        public decimal Tax { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal ServiceFee { get; set; }
        public decimal FinalTotal { get; set; }
        public List<CustomerOrderCalculationItemDto> Items { get; set; } = new();
    }

    public class CustomerOrderCalculationItemDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal ModifiersTotal { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class CustomerOrderTrackingDto
    {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CurrentStatus { get; set; } = string.Empty;
        public DateTime? Eta { get; set; }
        public List<CustomerOrderTimelineItemDto> Timeline { get; set; } = new();
    }

    public class CustomerOrderTimelineItemDto
    {
        public string Status { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public bool IsCurrent { get; set; }
        public DateTime? OccurredAt { get; set; }
    }

    public class CustomerDeviceRegisterRequest
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public string DeviceId { get; set; } = string.Empty;

        [StringLength(20)]
        public string? DeviceType { get; set; }

        [StringLength(500)]
        public string? FcmToken { get; set; }
    }

    public class CustomerDeviceDto
    {
        public Guid Id { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public bool HasFcmToken { get; set; }
        public DateTime LastSeenAt { get; set; }
    }

    public class CustomerMobileActionResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class CustomerFollowUsDto
    {
        public string? Facebook { get; set; }
        public string? Instagram { get; set; }
        public string? TikTok { get; set; }
        public string? Snapchat { get; set; }
        public string? X { get; set; }
        public string? YouTube { get; set; }
        public string? Website { get; set; }
        public string? GoogleMaps { get; set; }
    }

    public class CustomerContactDto
    {
        public string? Phone { get; set; }
        public string? WhatsApp { get; set; }
        public string? Email { get; set; }
        public string? WorkingHours { get; set; }
        public string? Address { get; set; }
        public string? AddressAr { get; set; }
    }

    public class CustomerContentPageDto
    {
        public string Title { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string ContentAr { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
    }

    public class CustomerLoyaltySummaryDto
    {
        public Guid CustomerId { get; set; }
        public decimal AvailablePoints { get; set; }
        public decimal AvailableBalance { get; set; }
        public string Tier { get; set; } = string.Empty;
        public string TierAr { get; set; } = string.Empty;
        public string? NextTier { get; set; }
        public decimal PointsToNextTier { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class CustomerLoyaltyCalculateRequest
    {
        [Range(0.01, double.MaxValue)]
        public decimal OrderAmount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal PointsToUse { get; set; }
    }

    public class CustomerLoyaltyCalculationDto
    {
        public decimal PointsUsed { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal RemainingPoints { get; set; }
        public decimal FinalTotal { get; set; }
    }

    public class CustomerLoyaltyWalletDto
    {
        public Guid CustomerId { get; set; }
        public decimal AvailablePoints { get; set; }
        public decimal PendingPoints { get; set; }
        public decimal LifetimePoints { get; set; }
        public decimal RedeemedPoints { get; set; }
        public decimal ExpiredPoints { get; set; }
        public decimal AvailableBalance { get; set; }
        public string CurrencyCode { get; set; } = "JOD";
        public string Tier { get; set; } = string.Empty;
        public string TierAr { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
    }

    public class CustomerLoyaltyTransactionDto
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public decimal Points { get; set; }
        public decimal BalanceAfter { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class CustomerRewardDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string? Description { get; set; }
        public string? DescriptionAr { get; set; }
        public decimal PointsRequired { get; set; }
        public string Type { get; set; } = string.Empty;
        public decimal? RewardValue { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public Guid? ProductId { get; set; }
        public string? ProductName { get; set; }
        public bool Affordable { get; set; }
    }

    public class CustomerRewardRedemptionDto
    {
        public Guid Id { get; set; }
        public string RewardName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal PointsUsed { get; set; }
        public decimal DiscountAmount { get; set; }
        public DateTime RedeemedAt { get; set; }
    }

    public class CustomerNotificationDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? TitleAr { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? MessageAr { get; set; }
        public string Type { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
