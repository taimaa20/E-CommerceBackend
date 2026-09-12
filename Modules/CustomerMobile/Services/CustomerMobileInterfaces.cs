using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Modules.CustomerMobile.Services
{
    public interface ICustomerMobileContext
    {
        Guid GetCustomerId();
        Guid GetTenantId();
    }

    public interface ICustomerOtpProvider
    {
        Task SendAsync(CustomerAccount customer, string purpose, CancellationToken ct);
        Task<bool> VerifyAsync(CustomerAccount customer, string purpose, string otpCode, CancellationToken ct);
    }

    public interface ICustomerAuthService
    {
        Task<CustomerProfileDto> RegisterAsync(CustomerRegisterRequest request, CancellationToken ct);
        Task<CustomerLoginResponse> LoginAsync(CustomerLoginRequest request, CancellationToken ct);
        Task<CustomerLoginResponse> RefreshAsync(CustomerRefreshTokenRequest request, CancellationToken ct);
        Task LogoutAsync(CustomerLogoutRequest request, CancellationToken ct);
        Task ForgotPasswordAsync(CustomerForgotPasswordRequest request, CancellationToken ct);
        Task ResetPasswordAsync(CustomerResetPasswordRequest request, CancellationToken ct);
        Task VerifyOtpAsync(CustomerVerifyOtpRequest request, CancellationToken ct);
    }

    public interface ICustomerProfileService
    {
        Task<CustomerProfileDto> GetProfileAsync(CancellationToken ct);
        Task<CustomerProfileDto> UpdateProfileAsync(CustomerProfileUpdateRequest request, CancellationToken ct);
        Task<List<CustomerAddressDto>> GetAddressesAsync(CancellationToken ct);
        Task<CustomerAddressDto> AddAddressAsync(CustomerAddressRequest request, CancellationToken ct);
        Task<CustomerAddressDto> UpdateAddressAsync(Guid id, CustomerAddressRequest request, CancellationToken ct);
        Task DeleteAddressAsync(Guid id, CancellationToken ct);
        Task<CustomerMobileActionResponse> DeleteAccountAsync(CancellationToken ct);
    }

    public interface ICustomerCartService
    {
        Task<CustomerCartDto> GetCartAsync(CancellationToken ct);
        Task<CustomerCartDto> AddItemAsync(CustomerCartItemRequest request, CancellationToken ct);
        Task<CustomerCartDto> UpdateItemAsync(Guid itemId, CustomerCartItemUpdateRequest request, CancellationToken ct);
        Task<CustomerCartDto> RemoveItemAsync(Guid itemId, CancellationToken ct);
        Task ClearAsync(CancellationToken ct);
    }

    public interface ICustomerOrderService
    {
        Task<CustomerOrderCreateResponse> CreateOrderAsync(CustomerOrderCreateRequest request, CancellationToken ct);
        Task<PaginatedResponse<CustomerOrderListItemDto>> GetOrdersAsync(int page, int pageSize, CancellationToken ct);
        Task<CustomerOrderDetailsDto> GetOrderAsync(Guid id, CancellationToken ct);
    }

    public interface ICustomerHomeService
    {
        Task<CustomerHomeDto> GetHomeAsync(string language, CancellationToken ct);
        Task<CustomerSearchResponseDto> SearchAsync(string keyword, string language, CancellationToken ct);
        Task<PublicProductDto> GetProductAsync(Guid id, string language, CancellationToken ct);
    }

    public interface ICustomerDeliveryService
    {
        Task<CustomerDeliveryCheckResponse> CheckZoneAsync(CustomerDeliveryCheckRequest request, CancellationToken ct);
    }

    public interface ICustomerCheckoutService
    {
        Task<CustomerOrderCalculationResponse> CalculateAsync(CustomerOrderCalculateRequest request, CancellationToken ct);
        Task<List<PaymentMethodDto>> GetPaymentMethodsAsync(Guid branchId, CancellationToken ct);
    }

    public interface ICustomerMobileBranchService
    {
        Task<PaginatedResponse<CustomerMobileBranchDto>> GetActiveAsync(int page, int pageSize, CancellationToken ct);
        Task<List<PaymentMethodDto>> GetPaymentMethodsAsync(Guid branchId, CancellationToken ct);
    }

    public interface ICustomerOrderTrackingService
    {
        Task<CustomerOrderTrackingDto> GetTrackingAsync(Guid orderId, CancellationToken ct);
    }

    public interface ICustomerReorderService
    {
        Task<CustomerCartDto> ReorderAsync(Guid orderId, CancellationToken ct);
    }

    public interface ICustomerDeviceService
    {
        Task<CustomerDeviceDto> RegisterAsync(CustomerDeviceRegisterRequest request, CancellationToken ct);
    }

    public interface ICustomerOrderCancellationService
    {
        Task<CustomerCancelOrderResponse> CancelAsync(Guid orderId, CustomerCancelOrderRequest request, CancellationToken ct);
    }

    public interface ICustomerMobileSettingsService
    {
        Task<CustomerFollowUsDto> GetFollowUsAsync(CancellationToken ct);
        Task<CustomerContactDto> GetContactAsync(CancellationToken ct);
        Task<CustomerContentPageDto> GetTermsAsync(CancellationToken ct);
        Task<CustomerContentPageDto> GetPrivacyPolicyAsync(CancellationToken ct);
    }

    public interface ICustomerLoyaltyService
    {
        Task<CustomerLoyaltySummaryDto> GetSummaryAsync(CancellationToken ct);
        Task<CustomerLoyaltyCalculationDto> CalculateAsync(CustomerLoyaltyCalculateRequest request, CancellationToken ct);

        /// <summary>Wallet snapshot for the authenticated customer only.</summary>
        Task<CustomerLoyaltyWalletDto> GetWalletAsync(CancellationToken ct);

        /// <summary>Paginated wallet ledger for the authenticated customer only.</summary>
        Task<PaginatedResponse<CustomerLoyaltyTransactionDto>> GetTransactionsAsync(int page, int pageSize, CancellationToken ct);

        /// <summary>Active reward catalog, flagged by what the customer can currently afford.</summary>
        Task<List<CustomerRewardDto>> GetRewardsAsync(CancellationToken ct);

        /// <summary>The authenticated customer's own reward redemption history.</summary>
        Task<PaginatedResponse<CustomerRewardRedemptionDto>> GetRewardRedemptionsAsync(int page, int pageSize, CancellationToken ct);
    }

    public interface ICustomerNotificationService
    {
        Task<PaginatedResponse<CustomerNotificationDto>> GetNotificationsAsync(int page, int pageSize, CancellationToken ct);
        Task<CustomerMobileActionResponse> MarkAsReadAsync(Guid id, CancellationToken ct);
    }
}
