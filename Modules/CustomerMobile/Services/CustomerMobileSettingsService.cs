using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.CustomerMobile.Services
{
    public class CustomerMobileSettingsService : ICustomerMobileSettingsService
    {
        private readonly ISettingsService _settingsService;
        private readonly ICustomerMobileContext _mobileContext;

        public CustomerMobileSettingsService(
            ISettingsService settingsService,
            ICustomerMobileContext mobileContext)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _mobileContext = mobileContext ?? throw new ArgumentNullException(nameof(mobileContext));
        }

        public async Task<CustomerFollowUsDto> GetFollowUsAsync(CancellationToken ct)
        {
            var settings = await LoadSettingsAsync(ct);
            return new CustomerFollowUsDto
            {
                Facebook = settings.FacebookUrl,
                Instagram = settings.InstagramUrl,
                TikTok = settings.TikTokUrl,
                Snapchat = settings.SnapchatUrl,
                X = settings.XUrl,
                YouTube = settings.YouTubeUrl,
                Website = settings.WebsiteUrl,
                GoogleMaps = settings.GoogleMapsLocationUrl
            };
        }

        public async Task<CustomerContactDto> GetContactAsync(CancellationToken ct)
        {
            var settings = await LoadSettingsAsync(ct);
            return new CustomerContactDto
            {
                Phone = settings.RestaurantPhone,
                WhatsApp = settings.WhatsAppNumber,
                Email = settings.RestaurantEmail,
                WorkingHours = settings.WorkingHours,
                Address = settings.RestaurantAddress,
                AddressAr = settings.RestaurantAddressAr
            };
        }

        public async Task<CustomerContentPageDto> GetTermsAsync(CancellationToken ct)
        {
            var settings = await LoadSettingsAsync(ct);
            return new CustomerContentPageDto
            {
                Title = settings.TermsTitle,
                TitleAr = settings.TermsTitleAr,
                Content = settings.TermsContent ?? string.Empty,
                ContentAr = settings.TermsContentAr ?? string.Empty,
                LastUpdated = settings.UpdatedAt
            };
        }

        public async Task<CustomerContentPageDto> GetPrivacyPolicyAsync(CancellationToken ct)
        {
            var settings = await LoadSettingsAsync(ct);
            return new CustomerContentPageDto
            {
                Title = settings.PrivacyTitle,
                TitleAr = settings.PrivacyTitleAr,
                Content = settings.PrivacyContent ?? string.Empty,
                ContentAr = settings.PrivacyContentAr ?? string.Empty,
                LastUpdated = settings.UpdatedAt
            };
        }

        private Task<SystemSettings> LoadSettingsAsync(CancellationToken ct)
            => _settingsService.GetSettingsAsync(_mobileContext.GetTenantId(), ct);
    }
}
