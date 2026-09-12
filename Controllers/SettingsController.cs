using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;
using RestaurantPos.Api.Services.Time;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SettingsController : ControllerBase
    {
        private const string BranchCoordinatesRequiredMessage = "Branch latitude and longitude must be configured together.";
        private const string BranchRadiusRequiredMessage = "Allowed attendance radius is required when branch coordinates are configured.";

        private readonly ISettingsService _settingsService;
        private readonly ITenantResolver _tenantResolver;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(
            ISettingsService settingsService,
            ITenantResolver tenantResolver,
            ILogger<SettingsController> logger)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: api/settings (public read — settings are not sensitive, updates are Admin-only)
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<SettingsDto>> GetSettings()
        {
            try
            {
                var settings = await _settingsService.GetSettingsAsync(
                    _tenantResolver.GetTenantId(),
                    HttpContext.RequestAborted);
                return Ok(MapToDto(settings));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load system settings. Returning default settings response.");
                return Ok(new SettingsDto());
            }
        }

        // PUT: api/settings
        [HttpPut]
        [Authorize(Roles = AppRoleGroups.AdminOnly)]
        public async Task<ActionResult<SettingsDto>> UpdateSettings([FromBody] SettingsDto dto)
        {
            try
            {
                var tenantId = _tenantResolver.GetTenantId();
                var settings = await _settingsService.GetSettingsAsync(
                    tenantId,
                    HttpContext.RequestAborted);

                // Map DTO to entity
                settings.RestaurantName = dto.RestaurantName?.Trim() ?? "RestoPOS";
                settings.RestaurantTagline = dto.RestaurantTagline?.Trim();
                settings.LogoUrl = dto.LogoUrl?.Trim();
                settings.CoverImageUrl = dto.CoverImageUrl?.Trim();
                settings.RestaurantAddress = dto.RestaurantAddress?.Trim();
                settings.RestaurantAddressAr = dto.RestaurantAddressAr?.Trim();
                settings.RestaurantPhone = dto.RestaurantPhone?.Trim();
                settings.WhatsAppNumber = dto.WhatsAppNumber?.Trim();
                settings.RestaurantEmail = dto.RestaurantEmail?.Trim();
                settings.WebsiteUrl = dto.WebsiteUrl?.Trim();
                settings.TaxNumber = dto.TaxNumber?.Trim();
                settings.FacebookUrl = dto.FacebookUrl?.Trim();
                settings.InstagramUrl = dto.InstagramUrl?.Trim();
                settings.TikTokUrl = dto.TikTokUrl?.Trim();
                settings.SnapchatUrl = dto.SnapchatUrl?.Trim();
                settings.XUrl = dto.XUrl?.Trim();
                settings.YouTubeUrl = dto.YouTubeUrl?.Trim();
                settings.GoogleMapsLocationUrl = dto.GoogleMapsLocationUrl?.Trim();
                var branchLocationError = ValidateBranchLocation(dto);
                if (branchLocationError != null)
                {
                    return BadRequest(new { message = branchLocationError });
                }

                settings.Latitude = dto.Latitude;
                settings.Longitude = dto.Longitude;
                settings.AllowedRadiusMeters = dto.AllowedRadiusMeters;
                settings.GoogleReviewUrl = dto.GoogleReviewUrl?.Trim();
                settings.QrMenuUrl = dto.QrMenuUrl?.Trim();

                // Finance
                settings.Currency = dto.Currency ?? "JOD";
                settings.TaxRate = Math.Clamp(dto.TaxRate, 0, 100);
                settings.ServiceChargeRate = Math.Clamp(dto.ServiceChargeRate, 0, 100);
                settings.EnableDiscounts = dto.EnableDiscounts;
                settings.EnableLoyalty = dto.EnableLoyalty;
                if (dto.DuplicateInvoiceBehavior is not null)
                {
                    if (!Enum.TryParse<DuplicateInvoiceBehavior>(
                            dto.DuplicateInvoiceBehavior,
                            ignoreCase: true,
                            out var duplicateInvoiceBehavior)
                        || !Enum.IsDefined(duplicateInvoiceBehavior))
                    {
                        return BadRequest(new { message = "Invalid duplicate invoice behavior." });
                    }

                    settings.DuplicateInvoiceBehavior = duplicateInvoiceBehavior;
                }
                settings.LoyaltyPointValue = Math.Max(0, decimal.Round(dto.LoyaltyPointValue, 4, MidpointRounding.AwayFromZero));
                settings.LoyaltyMinimumRedeemPoints = NormalizePositiveDecimal(dto.LoyaltyMinimumRedeemPoints);
                settings.LoyaltyMaximumRedeemPoints = NormalizePositiveDecimal(dto.LoyaltyMaximumRedeemPoints);
                settings.LoyaltyBronzeThreshold = NormalizePositiveDecimal(dto.LoyaltyBronzeThreshold);
                settings.LoyaltySilverThreshold = NormalizePositiveDecimal(dto.LoyaltySilverThreshold);
                settings.LoyaltyGoldThreshold = NormalizePositiveDecimal(dto.LoyaltyGoldThreshold);
                settings.LoyaltyVipThreshold = NormalizePositiveDecimal(dto.LoyaltyVipThreshold);
                settings.VoucherEnabled = dto.VoucherEnabled;
                settings.VoucherAmount = decimal.Round(dto.VoucherAmount, 2, MidpointRounding.AwayFromZero);
                settings.VoucherDailyLimit = Math.Max(1, dto.VoucherDailyLimit);

                // Receipt
                settings.ReceiptFooterNote = dto.ReceiptFooterNote?.Trim() ?? "Thank you for your visit!";
                settings.WorkingHours = dto.WorkingHours?.Trim();
                settings.TermsTitle = NormalizeRequired(dto.TermsTitle, "Terms & Conditions");
                settings.TermsTitleAr = NormalizeRequired(dto.TermsTitleAr, "الشروط والأحكام");
                settings.TermsContent = dto.TermsContent?.Trim();
                settings.TermsContentAr = dto.TermsContentAr?.Trim();
                settings.PrivacyTitle = NormalizeRequired(dto.PrivacyTitle, "Privacy Policy");
                settings.PrivacyTitleAr = NormalizeRequired(dto.PrivacyTitleAr, "سياسة الخصوصية");
                settings.PrivacyContent = dto.PrivacyContent?.Trim();
                settings.PrivacyContentAr = dto.PrivacyContentAr?.Trim();
                settings.ReceiptCopies = Math.Clamp(dto.ReceiptCopies, 1, 3);
                settings.AutoPrintReceipt = dto.AutoPrintReceipt;
                settings.ShowTaxOnReceipt = dto.ShowTaxOnReceipt;
                settings.ShowLogoOnReceipt = dto.ShowLogoOnReceipt;

                // Notifications
                settings.NotifyNewOrder = dto.NotifyNewOrder;
                settings.NotifyOrderReady = dto.NotifyOrderReady;
                settings.NotifyLowStock = dto.NotifyLowStock;
                settings.SoundEnabled = dto.SoundEnabled;

                // Expiry / Waste
                settings.NearExpiryDays = Math.Clamp(dto.NearExpiryDays, 1, 30);
                settings.ExpiryJobRunTime = string.IsNullOrWhiteSpace(dto.ExpiryJobRunTime)
                    ? "01:00"
                    : dto.ExpiryJobRunTime.Trim();
                settings.ExpiryNotifyRoles = string.IsNullOrWhiteSpace(dto.ExpiryNotifyRoles)
                    ? "Admin,Manager"
                    : dto.ExpiryNotifyRoles.Trim();

                // System
                settings.Language = dto.Language ?? "en";
                if (!RestaurantTimeZone.IsValid(dto.TimeZoneId))
                    return BadRequest(new { message = "Invalid time zone identifier." });

                settings.TimeZoneId = string.IsNullOrWhiteSpace(dto.TimeZoneId)
                    ? RestaurantTimeDefaults.TimeZoneId
                    : dto.TimeZoneId.Trim();
                settings.OrderPrepTimeout = Math.Clamp(dto.OrderPrepTimeout, 5, 120);
                settings.KitchenDisplayEnabled = dto.KitchenDisplayEnabled;
                settings.AllowDirectCancel = dto.AllowDirectCancel ?? settings.AllowDirectCancel;
                settings.AllowMobileCancelPreparing = dto.AllowMobileCancelPreparing;
                settings.TableAutoRelease = dto.TableAutoRelease;

                var updated = await _settingsService.UpdateSettingsAsync(
                    settings,
                    tenantId,
                    HttpContext.RequestAborted);
                return Ok(MapToDto(updated));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update system settings.");
                return StatusCode(500, new { message = "Internal server error", detail = ex.Message });
            }
        }

        private static SettingsDto MapToDto(SystemSettings s) => new SettingsDto
        {
            // Restaurant
            RestaurantName = s.RestaurantName,
            RestaurantTagline = s.RestaurantTagline,
            LogoUrl = s.LogoUrl,
            CoverImageUrl = s.CoverImageUrl,
            RestaurantAddress = s.RestaurantAddress,
            RestaurantAddressAr = s.RestaurantAddressAr,
            RestaurantPhone = s.RestaurantPhone,
            WhatsAppNumber = s.WhatsAppNumber,
            RestaurantEmail = s.RestaurantEmail,
            WebsiteUrl = s.WebsiteUrl,
            TaxNumber = s.TaxNumber,
            FacebookUrl = s.FacebookUrl,
            InstagramUrl = s.InstagramUrl,
            TikTokUrl = s.TikTokUrl,
            SnapchatUrl = s.SnapchatUrl,
            XUrl = s.XUrl,
            YouTubeUrl = s.YouTubeUrl,
            GoogleMapsLocationUrl = s.GoogleMapsLocationUrl,
            Latitude = s.Latitude,
            Longitude = s.Longitude,
            AllowedRadiusMeters = s.AllowedRadiusMeters,
            GoogleReviewUrl = s.GoogleReviewUrl,
            QrMenuUrl = s.QrMenuUrl,
            // Finance
            Currency = s.Currency,
            TaxRate = s.TaxRate,
            ServiceChargeRate = s.ServiceChargeRate,
            EnableDiscounts = s.EnableDiscounts,
            EnableLoyalty = s.EnableLoyalty,
            DuplicateInvoiceBehavior = s.DuplicateInvoiceBehavior.ToString(),
            LoyaltyPointValue = s.LoyaltyPointValue,
            LoyaltyMinimumRedeemPoints = s.LoyaltyMinimumRedeemPoints,
            LoyaltyMaximumRedeemPoints = s.LoyaltyMaximumRedeemPoints,
            LoyaltyBronzeThreshold = s.LoyaltyBronzeThreshold,
            LoyaltySilverThreshold = s.LoyaltySilverThreshold,
            LoyaltyGoldThreshold = s.LoyaltyGoldThreshold,
            LoyaltyVipThreshold = s.LoyaltyVipThreshold,
            VoucherEnabled = s.VoucherEnabled,
            VoucherAmount = s.VoucherAmount,
            VoucherDailyLimit = s.VoucherDailyLimit,
            // Receipt
            ReceiptFooterNote = s.ReceiptFooterNote,
            WorkingHours = s.WorkingHours,
            TermsTitle = s.TermsTitle,
            TermsTitleAr = s.TermsTitleAr,
            TermsContent = s.TermsContent,
            TermsContentAr = s.TermsContentAr,
            PrivacyTitle = s.PrivacyTitle,
            PrivacyTitleAr = s.PrivacyTitleAr,
            PrivacyContent = s.PrivacyContent,
            PrivacyContentAr = s.PrivacyContentAr,
            ReceiptCopies = s.ReceiptCopies,
            AutoPrintReceipt = s.AutoPrintReceipt,
            ShowTaxOnReceipt = s.ShowTaxOnReceipt,
            ShowLogoOnReceipt = s.ShowLogoOnReceipt,
            // Notifications
            NotifyNewOrder = s.NotifyNewOrder,
            NotifyOrderReady = s.NotifyOrderReady,
            NotifyLowStock = s.NotifyLowStock,
            SoundEnabled = s.SoundEnabled,
            NearExpiryDays = s.NearExpiryDays,
            ExpiryJobRunTime = s.ExpiryJobRunTime,
            ExpiryNotifyRoles = s.ExpiryNotifyRoles,
            // System
            Language = s.Language,
            TimeZoneId = s.TimeZoneId,
            OrderPrepTimeout = s.OrderPrepTimeout,
            KitchenDisplayEnabled = s.KitchenDisplayEnabled,
            AllowDirectCancel = s.AllowDirectCancel,
            AllowMobileCancelPreparing = s.AllowMobileCancelPreparing,
            TableAutoRelease = s.TableAutoRelease,
            // Meta
            UpdatedAt = s.UpdatedAt.ToString("O")
        };

        private static decimal? NormalizePositiveDecimal(decimal? value)
            => value.HasValue && value.Value > 0 ? decimal.Round(value.Value, 2, MidpointRounding.AwayFromZero) : null;

        private static string NormalizeRequired(string? value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        private static string? ValidateBranchLocation(SettingsDto dto)
        {
            var hasAnyLocationSetting =
                dto.Latitude.HasValue
                || dto.Longitude.HasValue
                || dto.AllowedRadiusMeters.HasValue;

            if (!hasAnyLocationSetting)
            {
                return null;
            }

            if (!dto.Latitude.HasValue || !dto.Longitude.HasValue)
            {
                return BranchCoordinatesRequiredMessage;
            }

            return dto.AllowedRadiusMeters.HasValue ? null : BranchRadiusRequiredMessage;
        }
    }

    public class SettingsDto
    {
        // Restaurant
        [MaxLength(200)]
        public string RestaurantName { get; set; } = "RestoPOS";
        [MaxLength(300)]
        public string? RestaurantTagline { get; set; }
        [MaxLength(500)]
        public string? LogoUrl { get; set; }
        [MaxLength(500)]
        public string? CoverImageUrl { get; set; }
        [MaxLength(500)]
        public string? RestaurantAddress { get; set; }
        [MaxLength(500)]
        public string? RestaurantAddressAr { get; set; }
        [MaxLength(50)]
        public string? RestaurantPhone { get; set; }
        [MaxLength(50)]
        public string? WhatsAppNumber { get; set; }
        [MaxLength(200)]
        [EmailAddress]
        public string? RestaurantEmail { get; set; }
        [MaxLength(500)]
        public string? WebsiteUrl { get; set; }
        [MaxLength(50)]
        public string? TaxNumber { get; set; }
        [MaxLength(500)]
        public string? FacebookUrl { get; set; }
        [MaxLength(500)]
        public string? InstagramUrl { get; set; }
        [MaxLength(500)]
        public string? TikTokUrl { get; set; }
        [MaxLength(500)]
        public string? SnapchatUrl { get; set; }
        [MaxLength(500)]
        public string? XUrl { get; set; }
        [MaxLength(500)]
        public string? YouTubeUrl { get; set; }
        [MaxLength(500)]
        public string? GoogleMapsLocationUrl { get; set; }
        [Range(-90, 90)]
        public decimal? Latitude { get; set; }
        [Range(-180, 180)]
        public decimal? Longitude { get; set; }
        [Range(1, int.MaxValue)]
        public int? AllowedRadiusMeters { get; set; }
        [MaxLength(500)]
        public string? GoogleReviewUrl { get; set; }
        [MaxLength(500)]
        public string? QrMenuUrl { get; set; }

        // Finance
        public string Currency { get; set; } = "JOD";
        [Range(0, 100)]
        public decimal TaxRate { get; set; } = 16;
        [Range(0, 100)]
        public decimal ServiceChargeRate { get; set; } = 0;
        public bool EnableDiscounts { get; set; } = true;
        public bool EnableLoyalty { get; set; } = true;
        public string? DuplicateInvoiceBehavior { get; set; }
        [Range(0, double.MaxValue)]
        public decimal LoyaltyPointValue { get; set; } = 0m;
        [Range(0, double.MaxValue)]
        public decimal? LoyaltyMinimumRedeemPoints { get; set; }
        [Range(0, double.MaxValue)]
        public decimal? LoyaltyMaximumRedeemPoints { get; set; }
        [Range(0, double.MaxValue)]
        public decimal? LoyaltyBronzeThreshold { get; set; }
        [Range(0, double.MaxValue)]
        public decimal? LoyaltySilverThreshold { get; set; }
        [Range(0, double.MaxValue)]
        public decimal? LoyaltyGoldThreshold { get; set; }
        [Range(0, double.MaxValue)]
        public decimal? LoyaltyVipThreshold { get; set; }
        public bool VoucherEnabled { get; set; } = VoucherDefaults.Enabled;
        [Range(0.01, double.MaxValue)]
        public decimal VoucherAmount { get; set; } = VoucherDefaults.Amount;
        [Range(1, int.MaxValue)]
        public int VoucherDailyLimit { get; set; } = VoucherDefaults.DailyLimit;

        // Receipt
        [MaxLength(500)]
        public string ReceiptFooterNote { get; set; } = "Thank you for your visit!";
        [MaxLength(200)]
        public string? WorkingHours { get; set; }
        [MaxLength(200)]
        public string TermsTitle { get; set; } = "Terms & Conditions";
        [MaxLength(200)]
        public string TermsTitleAr { get; set; } = "الشروط والأحكام";
        public string? TermsContent { get; set; }
        public string? TermsContentAr { get; set; }
        [MaxLength(200)]
        public string PrivacyTitle { get; set; } = "Privacy Policy";
        [MaxLength(200)]
        public string PrivacyTitleAr { get; set; } = "سياسة الخصوصية";
        public string? PrivacyContent { get; set; }
        public string? PrivacyContentAr { get; set; }
        [Range(1, 3)]
        public int ReceiptCopies { get; set; } = 1;
        public bool AutoPrintReceipt { get; set; } = false;
        public bool ShowTaxOnReceipt { get; set; } = true;
        public bool ShowLogoOnReceipt { get; set; } = true;

        // Notifications
        public bool NotifyNewOrder { get; set; } = true;
        public bool NotifyOrderReady { get; set; } = true;
        public bool NotifyLowStock { get; set; } = true;
        public bool SoundEnabled { get; set; } = true;

        // Expiry / Waste
        [Range(1, 30)]
        public int NearExpiryDays { get; set; } = 3;
        [MaxLength(10)]
        public string ExpiryJobRunTime { get; set; } = "01:00";
        [MaxLength(100)]
        public string ExpiryNotifyRoles { get; set; } = "Admin,Manager";

        // System
        public string Language { get; set; } = "en";
        [MaxLength(100)]
        public string TimeZoneId { get; set; } = RestaurantTimeDefaults.TimeZoneId;
        [Range(5, 120)]
        public int OrderPrepTimeout { get; set; } = 30;
        public bool KitchenDisplayEnabled { get; set; } = true;
        public bool? AllowDirectCancel { get; set; }
        public bool AllowMobileCancelPreparing { get; set; } = false;
        public bool TableAutoRelease { get; set; } = false;

        // Meta
        public string? UpdatedAt { get; set; }
    }
}
