using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.DTOs.Auth;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Interfaces;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Modules.CustomerMobile.Services;

namespace RestaurantPos.Api.Controllers.Mobile
{
    [ApiController]
    [Route("api/mobile/auth")]
    public class MobileAuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ICustomerAuthService _customerAuthService;
        private readonly ICurrentUserResolver _currentUserResolver;

        public MobileAuthController(
            IAuthService authService,
            ICustomerAuthService customerAuthService,
            ICurrentUserResolver currentUserResolver)
        {
            _authService = authService;
            _customerAuthService = customerAuthService;
            _currentUserResolver = currentUserResolver;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "البيانات مطلوبة / Username and password are required" });
            }

            var response = await _authService.LoginAsync(request);
            if (response != null)
            {
                return Ok(response);
            }

            try
            {
                return Ok(await _customerAuthService.LoginAsync(new CustomerLoginRequest
                {
                    Identifier = request.Username,
                    Password = request.Password,
                    DeviceId = request.DeviceId,
                    DeviceName = request.DeviceName
                }, HttpContext.RequestAborted));
            }
            catch (UnauthorizedException)
            {
                return Unauthorized(new { message = "بيانات غير صحيحة / Invalid credentials" });
            }
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(new { message = "رمز التحديث مطلوب / Refresh token is required" });
            }

            var response = await _authService.RefreshAsync(request);
            if (response != null)
            {
                return Ok(response);
            }

            try
            {
                return Ok(await _customerAuthService.RefreshAsync(new CustomerRefreshTokenRequest
                {
                    RefreshToken = request.RefreshToken,
                    DeviceId = request.DeviceId
                }, HttpContext.RequestAborted));
            }
            catch (UnauthorizedException)
            {
                return Unauthorized(new { message = "انتهت الجلسة / Session expired" });
            }
        }

        [HttpPost("logout")]
        [Authorize]
        [EnableRateLimiting("api")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            if (ModelState.IsValid && !string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                await _authService.LogoutAsync(request);
                await _customerAuthService.LogoutAsync(new CustomerLogoutRequest
                {
                    RefreshToken = request.RefreshToken
                }, HttpContext.RequestAborted);
            }

            return Ok(new { message = "تم تسجيل الخروج / Logged out" });
        }

        [HttpGet("me")]
        [Authorize]
        [EnableRateLimiting("api")]
        public IActionResult Me()
        {
            var userId = _currentUserResolver.ResolveUserIdAsync(User).GetAwaiter().GetResult();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "جلسة غير صالحة / Invalid session" });
            }

            return Ok(new AuthUserDto
            {
                Id = userId.Value,
                Username = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                FullName = User.FindFirstValue("full_name"),
                FullNameAr = User.FindFirstValue("full_name_ar"),
                Role = User.FindFirstValue(ClaimTypes.Role)
            });
        }
    }
}
