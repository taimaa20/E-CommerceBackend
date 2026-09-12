using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.DTOs.Auth;
using RestaurantPos.Api.Interfaces;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger      = logger      ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("login")]
        public async Task<ActionResult<LegacyLoginResponse>> Login(
            [FromBody] LegacyLoginRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var response = await _authService.LegacyLoginAsync(request, cancellationToken);
            if (response == null)
            {
                _logger.LogWarning("Login failed for {Username}", request.Username);
                return Unauthorized("Kullanıcı adı veya şifre hatalı.");
            }

            _logger.LogInformation(
                "Login succeeded for user {UserId} ({Username}) role={Role}",
                response.UserId, response.Username, response.Role);

            return Ok(response);
        }
    }
}
