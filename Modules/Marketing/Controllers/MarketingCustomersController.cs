using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Modules.Marketing.DTOs;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.Marketing.Controllers
{
    /// <summary>Sensitive customer-level loyalty operations (merge). Admin-only.</summary>
    [ApiController]
    [Route("api/v1/marketing/customers")]
    [Authorize(Roles = AppRoleGroups.AdminStrict)]
    public sealed class MarketingCustomersController : ControllerBase
    {
        private readonly ICustomerMergeService _merge;

        public MarketingCustomersController(ICustomerMergeService merge)
            => _merge = merge ?? throw new ArgumentNullException(nameof(merge));

        [HttpGet("merge/preview")]
        public async Task<ActionResult<CustomerMergePreviewDto>> Preview(
            [FromQuery] Guid survivorCustomerId, [FromQuery] Guid mergedCustomerId, CancellationToken ct)
            => Ok(await _merge.PreviewAsync(survivorCustomerId, mergedCustomerId, GeneralHelper.IsArabicRequested(Request), ct));

        [HttpPost("merge")]
        public async Task<ActionResult<CustomerMergeHistoryDto>> Merge([FromBody] MergeCustomersRequest request, CancellationToken ct)
        {
            var history = await _merge.MergeAsync(request.SurvivorCustomerId, request.MergedCustomerId, ct);
            return Ok(new CustomerMergeHistoryDto
            {
                Id = history.Id,
                SurvivorCustomerId = history.SurvivorCustomerId,
                MergedCustomerId = history.MergedCustomerId,
                PointsTransferred = history.PointsTransferred,
                MergedAt = history.MergedAt,
                Status = history.Status.ToString()
            });
        }
    }
}
