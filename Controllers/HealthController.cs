using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/health")]
    [AllowAnonymous]
    public class HealthController : ControllerBase
    {
        private static readonly TimeSpan DatabaseProbeTimeout = TimeSpan.FromSeconds(5);

        private readonly PosDbContext _context;
        private readonly ILogger<HealthController> _logger;

        public HealthController(PosDbContext context, ILogger<HealthController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public IActionResult Get()
            => Ok(new { status = "ok" });

        [HttpGet("db")]
        public async Task<IActionResult> GetDatabase(CancellationToken ct)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(DatabaseProbeTimeout);

            try
            {
                var canConnect = await _context.Database.CanConnectAsync(timeout.Token);
                return canConnect
                    ? Ok(new { status = "ok" })
                    : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "unavailable" });
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout, new { status = "timeout" });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Database health probe failed.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "unavailable" });
            }
        }
    }
}
