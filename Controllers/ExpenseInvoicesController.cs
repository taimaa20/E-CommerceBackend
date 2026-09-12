using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.DTOs.ExpenseInvoice;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;
using RestaurantPos.Api.Services.Storage;

namespace RestaurantPos.Api.Controllers
{
    /// <summary>
    /// Expense / supplier / store-expense invoices.
    /// Restricted to Admin + Manager — these rows have direct financial impact.
    /// </summary>
    [ApiController]
    [Route("api/expense-invoices")]
    [Authorize(Roles = AppRoleGroups.AdminOnly)]
    public sealed class ExpenseInvoicesController : ControllerBase
    {
        // Hard cap on body size for the upload endpoint. Mirrors LocalFileStorageService.MaxFileSize.
        private const long UploadMaxBytes = LocalFileStorageService.MaxFileSize;

        private readonly IExpenseInvoiceService _service;

        public ExpenseInvoicesController(IExpenseInvoiceService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        // GET: api/expense-invoices
        [HttpGet]
        public async Task<ActionResult<PaginatedResponse<ExpenseInvoiceListItemDto>>> GetPaged(
            [FromQuery] ExpenseInvoiceQueryDto query,
            CancellationToken ct)
        {
            var result = await _service.GetPagedAsync(query, GeneralHelper.IsArabicRequested(Request), ct);
            return Ok(result);
        }

        // GET: api/expense-invoices/statistics
        [HttpGet("statistics")]
        public async Task<ActionResult<ExpenseInvoiceStatisticsDto>> GetStatistics(
            [FromQuery] Guid? branchId,
            CancellationToken ct)
        {
            var stats = await _service.GetStatisticsAsync(branchId, ct);
            return Ok(stats);
        }

        // GET: api/expense-invoices/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ExpenseInvoiceDetailsDto>> GetById(Guid id, CancellationToken ct)
        {
            var dto = await _service.GetDetailsAsync(id, GeneralHelper.IsArabicRequested(Request), ct);
            return Ok(dto);
        }

        // POST: api/expense-invoices
        [HttpPost]
        public async Task<ActionResult<ExpenseInvoiceDetailsDto>> Create(
            [FromBody] CreateExpenseInvoiceDto dto,
            CancellationToken ct)
        {
            var created = await _service.CreateAsync(
                dto,
                GetCurrentUserId(),
                GetCurrentUserName(),
                GeneralHelper.IsArabicRequested(Request),
                ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        // PUT: api/expense-invoices/{id}
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ExpenseInvoiceDetailsDto>> Update(
            Guid id,
            [FromBody] UpdateExpenseInvoiceDto dto,
            CancellationToken ct)
        {
            var updated = await _service.UpdateAsync(
                id,
                dto,
                GetCurrentUserId(),
                GetCurrentUserName(),
                GeneralHelper.IsArabicRequested(Request),
                ct);
            return Ok(updated);
        }

        [HttpPut("{id:guid}/payment")]
        public async Task<ActionResult<ExpenseInvoiceDetailsDto>> UpdatePayment(
            Guid id,
            [FromBody] UpdateExpenseInvoicePaymentDto dto,
            CancellationToken ct)
        {
            var updated = await _service.UpdatePaymentAsync(
                id,
                dto,
                GeneralHelper.IsArabicRequested(Request),
                ct);
            return Ok(updated);
        }

        // DELETE: api/expense-invoices/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await _service.SoftDeleteAsync(id, ct);
            return NoContent();
        }

        // POST: api/expense-invoices/{id}/attachments
        [HttpPost("{id:guid}/attachments")]
        [RequestSizeLimit(UploadMaxBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = UploadMaxBytes)]
        public async Task<ActionResult<ExpenseInvoiceAttachmentDto>> UploadAttachment(
            Guid id,
            IFormFile file,
            CancellationToken ct)
        {
            var dto = await _service.AddAttachmentAsync(id, file, GetCurrentUserId(), ct);
            return Ok(dto);
        }

        // DELETE: api/expense-invoices/{id}/attachments/{attachmentId}
        [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
        public async Task<IActionResult> DeleteAttachment(Guid id, Guid attachmentId, CancellationToken ct)
        {
            await _service.DeleteAttachmentAsync(id, attachmentId, ct);
            return NoContent();
        }

        // GET: api/expense-invoices/{id}/attachments/{attachmentId}/download
        // Authorised + tenant-scoped download. Streams the file — never loads
        // the full payload into memory.
        [HttpGet("{id:guid}/attachments/{attachmentId:guid}/download")]
        public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken ct)
        {
            var (stream, fileName, mimeType) = await _service.OpenAttachmentForDownloadAsync(id, attachmentId, ct);

            // enableRangeProcessing = true so large PDFs can be progressively
            // rendered by the browser (Adobe / Chrome PDF viewer use ranges).
            return File(stream, mimeType, fileName, enableRangeProcessing: true);
        }

        // ──────────────────────── helpers ──────────────────────────────────

        private Guid? GetCurrentUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("UserId")
                ?? User.FindFirstValue("userId");

            return Guid.TryParse(raw, out var id) ? id : null;
        }

        private string? GetCurrentUserName()
            => User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue("name")
                ?? User.Identity?.Name;
    }
}
