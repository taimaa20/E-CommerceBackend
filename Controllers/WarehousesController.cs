using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = AppRoleGroups.AdminOnly)]
    public class WarehousesController : ControllerBase
    {
        private readonly IWarehouseService _warehouseService;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICurrentUserAccessor _currentUser;

        public WarehousesController(
            IWarehouseService warehouseService,
            ITenantResolver tenantResolver,
            ICurrentUserAccessor currentUser)
        {
            _warehouseService = warehouseService ?? throw new ArgumentNullException(nameof(warehouseService));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var list = await _warehouseService.GetAllAsync(GeneralHelper.IsArabicRequested(Request), ct);
            return Ok(list);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var dto = await _warehouseService.GetByIdAsync(id, GeneralHelper.IsArabicRequested(Request), ct);
            return dto == null ? NotFound() : Ok(dto);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] WarehouseCreateDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var created = await _warehouseService.CreateAsync(dto, _tenantResolver.GetTenantId(), GeneralHelper.IsArabicRequested(Request), ct);
                return Ok(created);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] WarehouseUpdateDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var updated = await _warehouseService.UpdateAsync(id, dto, GeneralHelper.IsArabicRequested(Request), ct);
                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                await _warehouseService.DeleteAsync(id, ct);
                return Ok(new { message = "Warehouse deleted." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ── Inventory rows ────────────────────────────────────────────────────
        [HttpGet("inventory")]
        public async Task<IActionResult> GetInventory(
            [FromQuery] Guid? warehouseId,
            [FromQuery] Guid? rawMaterialId,
            CancellationToken ct)
        {
            var rows = await _warehouseService.GetInventoryAsync(warehouseId, rawMaterialId, GeneralHelper.IsArabicRequested(Request), ct);
            return Ok(rows);
        }

        [HttpPut("inventory")]
        public async Task<IActionResult> UpsertInventory([FromBody] RawMaterialInventoryUpsertDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var row = await _warehouseService.UpsertInventoryAsync(dto, _tenantResolver.GetTenantId(), GeneralHelper.IsArabicRequested(Request), ct);
                return Ok(row);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ── Transfers ─────────────────────────────────────────────────────────
        [HttpPost("transfers")]
        public async Task<IActionResult> CreateTransfer([FromBody] InventoryTransferCreateDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var performedById = _currentUser.UserId == Guid.Empty ? (Guid?)null : _currentUser.UserId;
                var transfer = await _warehouseService.CreateTransferAsync(
                    dto, _tenantResolver.GetTenantId(), performedById, GeneralHelper.IsArabicRequested(Request), ct);
                return Ok(transfer);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("transfers")]
        public async Task<IActionResult> GetTransfers(
            [FromQuery] Guid? warehouseId,
            [FromQuery] Guid? rawMaterialId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            CancellationToken ct)
        {
            var transfers = await _warehouseService.GetTransfersAsync(warehouseId, rawMaterialId, from, to, GeneralHelper.IsArabicRequested(Request), ct);
            return Ok(transfers);
        }

        // ── Reports ───────────────────────────────────────────────────────────
        [HttpGet("reports/stock-by-warehouse")]
        public async Task<IActionResult> StockByWarehouse(
            [FromQuery] Guid? warehouseId,
            [FromQuery] bool lowOnly = false,
            CancellationToken ct = default)
        {
            var rows = await _warehouseService.GetStockByWarehouseAsync(warehouseId, lowOnly, GeneralHelper.IsArabicRequested(Request), ct);
            return Ok(rows);
        }
    }
}
