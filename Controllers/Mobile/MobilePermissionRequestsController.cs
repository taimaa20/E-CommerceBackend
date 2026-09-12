using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.DTOs.MobileRequests;
using RestaurantPos.Api.Interfaces;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers.Mobile
{
    [ApiController]
    [Route("api/mobile/permission-requests")]
    [Authorize]
    [EnableRateLimiting("api")]
    public class MobilePermissionRequestsController : ControllerBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly IMobilePermissionRequestService _service;
        private readonly ICurrentUserResolver _currentUserResolver;

        public MobilePermissionRequestsController(
            IMobilePermissionRequestService service,
            ICurrentUserResolver currentUserResolver)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _currentUserResolver = currentUserResolver ?? throw new ArgumentNullException(nameof(currentUserResolver));
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] MobileRequestListQuery query, CancellationToken ct)
        {
            var userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue) return Unauthorized(new { message = "جلسة غير صالحة / Invalid session" });

            try
            {
                var data = await _service.GetAsync(userId.Value, query, ct);
                return Ok(new MobileDataResponse<IReadOnlyList<MobilePermissionRequestListItemDto>>(data));
            }
            catch (MobileRequestValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost]
        [Consumes("application/json", "multipart/form-data")]
        [RequestSizeLimit(MobilePermissionPolicies.MaxMultipartBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MobilePermissionPolicies.MaxMultipartBytes)]
        public async Task<IActionResult> Create(CancellationToken ct)
        {
            PermissionCreateInput input;
            try
            {
                input = await ReadCreateInputAsync(ct);
            }
            catch (JsonException)
            {
                return BadRequest(new { message = "Invalid JSON request body." });
            }

            return await Create(input.Request, input.Attachment, ct);
        }

        [HttpGet("allowance")]
        public async Task<IActionResult> Allowance(CancellationToken ct)
        {
            var userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue) return Unauthorized(new { message = "جلسة غير صالحة / Invalid session" });

            try
            {
                var data = await _service.GetAllowanceAsync(userId.Value, ct);
                return Ok(new MobileDataResponse<MobilePermissionAllowanceDto>(data));
            }
            catch (MobileRequestValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private async Task<IActionResult> Create(
            MobilePermissionRequestCreateDto? request,
            IFormFile? attachment,
            CancellationToken ct)
        {
            if (request == null) return BadRequest(new { message = "Request body is required." });
            var userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue) return Unauthorized(new { message = "جلسة غير صالحة / Invalid session" });

            try
            {
                var data = await _service.CreateAsync(userId.Value, request, attachment, ct);
                return Ok(new MobileDataResponse<MobilePermissionRequestDto>(data));
            }
            catch (MobileRequestValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private Task<Guid?> GetCurrentUserIdAsync()
            => _currentUserResolver.ResolveUserIdAsync(User);

        private async Task<PermissionCreateInput> ReadCreateInputAsync(CancellationToken ct)
        {
            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync(ct);
                return new PermissionCreateInput(
                    new MobilePermissionRequestCreateDto
                    {
                        Date = form["date"].FirstOrDefault(),
                        PermissionType = form["permission_type"].FirstOrDefault(),
                        TimeFrom = form["time_from"].FirstOrDefault(),
                        TimeTo = form["time_to"].FirstOrDefault(),
                        Reason = form["reason"].FirstOrDefault()
                    },
                    form.Files.GetFile("attachment") ?? form.Files.GetFile("file"));
            }

            var request = await JsonSerializer.DeserializeAsync<MobilePermissionRequestCreateDto>(
                Request.Body,
                JsonOptions,
                ct);
            return new PermissionCreateInput(request, null);
        }

        private sealed record PermissionCreateInput(
            MobilePermissionRequestCreateDto? Request,
            IFormFile? Attachment);
    }
}
