using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Modules.CustomerMobile.Services;

namespace RestaurantPos.Api.Modules.CustomerMobile.Controllers
{
    [ApiController]
    [Route("api/mobile/auth")]
    public class CustomerAuthController : ControllerBase
    {
        private readonly ICustomerAuthService _authService;
        private readonly ICustomerPhoneAuthService _phoneAuthService;

        public CustomerAuthController(
            ICustomerAuthService authService,
            ICustomerPhoneAuthService phoneAuthService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _phoneAuthService = phoneAuthService ?? throw new ArgumentNullException(nameof(phoneAuthService));
        }

        // ─── Single-entry phone-OTP flow ────────────────────────────────────────

        /// <summary>Step 1 — phone-based start. Looks up the customer, creates a stub
        /// record on first contact, sends an OTP, and returns the existing/new flag
        /// plus the current phone-verified / profile-completed flags. Until SMS
        /// provider integration ships the OTP value is fixed to "0000".</summary>
        [HttpPost("")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> StartAuth(CustomerStartAuthRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _phoneAuthService.StartAuthAsync(request, ct));
        }

        /// <summary>Step 2 — validates OTP and issues JWT. Response carries
        /// `nextAction` ("COMPLETE_PROFILE" or "HOME") so the mobile client can route
        /// straight to the right screen without an extra round-trip.</summary>
        [HttpPost("verify")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> VerifyAuth(CustomerVerifyAuthRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _phoneAuthService.VerifyAuthAsync(request, ct));
        }

        // ─── Legacy email/password endpoints (kept for backwards compatibility) ─

        [HttpPost("register")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [Obsolete("Replaced by phone OTP flow: POST /api/mobile/auth then /verify.")]
        public async Task<IActionResult> Register(CustomerRegisterRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _authService.RegisterAsync(request, ct));
        }

        [HttpPost("refresh-token")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> RefreshToken(CustomerRefreshTokenRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _authService.RefreshAsync(request, ct));
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [Obsolete("Phone OTP flow removes the password concept; kept for older clients.")]
        public async Task<IActionResult> ForgotPassword(CustomerForgotPasswordRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            await _authService.ForgotPasswordAsync(request, ct);
            return Ok(new { message = "If the account exists, a verification code was sent." });
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [Obsolete("Phone OTP flow removes the password concept; kept for older clients.")]
        public async Task<IActionResult> ResetPassword(CustomerResetPasswordRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            await _authService.ResetPasswordAsync(request, ct);
            return Ok(new { message = "Password reset successfully." });
        }
    }
}
