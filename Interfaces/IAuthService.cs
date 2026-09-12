using RestaurantPos.Api.DTOs.Auth;

namespace RestaurantPos.Api.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponse?> LoginAsync(LoginRequest request);
        Task<LoginResponse?> RefreshAsync(RefreshTokenRequest request);
        Task<bool> LogoutAsync(LogoutRequest request);
        Task RevokeAllUserTokensAsync(Guid userId);

        /// <summary>Legacy flat-token login used by the web client (POST /api/auth/login).
        /// Returns null if credentials are invalid. Issues a 24-h flat token with no refresh
        /// or device binding — kept separate from the mobile flow to preserve the contract.</summary>
        Task<LegacyLoginResponse?> LegacyLoginAsync(LegacyLoginRequest request, CancellationToken cancellationToken = default);
    }
}
