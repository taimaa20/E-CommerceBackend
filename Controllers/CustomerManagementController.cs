using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/customers")]
    [Authorize(Roles = AppRoleGroups.CustomerDirectoryViewers)]
    [EnableRateLimiting("api")]
    public sealed class CustomerManagementController : ControllerBase
    {
        private readonly ICustomerManagementService _service;

        public CustomerManagementController(ICustomerManagementService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [HttpGet]
        public async Task<ActionResult<PaginatedResponse<CustomerManagementListItemDto>>> GetPaged(
            [FromQuery] CustomerManagementQueryDto query,
            CancellationToken ct)
        {
            var result = await _service.GetPagedAsync(
                query,
                GeneralHelper.IsArabicRequested(Request),
                ct);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<CustomerManagementDetailsDto>> GetById(
            Guid id,
            [FromQuery] Guid? branchId,
            CancellationToken ct)
        {
            var result = await _service.GetByIdAsync(
                id,
                branchId,
                GeneralHelper.IsArabicRequested(Request),
                ct);
            return Ok(result);
        }

        [HttpGet("{id:guid}/orders")]
        public async Task<ActionResult<PaginatedResponse<CustomerManagementOrderDto>>> GetOrders(
            Guid id,
            [FromQuery] CustomerOrdersQueryDto query,
            CancellationToken ct)
        {
            var result = await _service.GetOrdersAsync(
                id,
                query,
                GeneralHelper.IsArabicRequested(Request),
                ct);
            return Ok(result);
        }
    }
}
