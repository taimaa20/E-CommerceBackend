using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services.Printing.Agent;

namespace RestaurantPos.Api.Controllers
{
    // Loopback-only print endpoint for offline mode. The cashier UI POSTs the
    // receipt HTML directly to the agent process when the cloud is unreachable,
    // bypassing the cloud DB / job queue. Reaches the same Puppeteer + SumatraPDF
    // or ESC/POS raster path as the online flow — just without the cloud round-trip.
    //
    // Bound to 127.0.0.1 by ASPNETCORE_URLS on the agent service. Even so, every
    // request also gets a runtime loopback check before it touches the printer.
    [Route("local/print")]
    [ApiController]
    [AllowAnonymous]
    public sealed class LocalPrintController : ControllerBase
    {
        private readonly AgentHtmlWriter _htmlWriter;
        private readonly ILogger<LocalPrintController> _logger;

        public LocalPrintController(AgentHtmlWriter htmlWriter, ILogger<LocalPrintController> logger)
        {
            _htmlWriter = htmlWriter ?? throw new ArgumentNullException(nameof(htmlWriter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("receipt-html")]
        public async Task<IActionResult> PrintReceiptHtml(
            [FromBody] LocalReceiptHtmlRequest request,
            CancellationToken ct)
        {
            if (!IsLoopback())
            {
                _logger.LogWarning(
                    "[LocalPrint] Rejected non-loopback request from {Remote}",
                    HttpContext.Connection.RemoteIpAddress);
                return NotFound();
            }

            if (request == null || string.IsNullOrWhiteSpace(request.HtmlContent))
                return BadRequest(new { error = "htmlContent is required." });

            var printerType = (PrinterType)request.PrinterType;
            if (printerType == PrinterType.Usb && string.IsNullOrWhiteSpace(request.WindowsPrinterName))
                return BadRequest(new { error = "windowsPrinterName is required for USB printers." });
            if (printerType == PrinterType.Network && string.IsNullOrWhiteSpace(request.IpAddress))
                return BadRequest(new { error = "ipAddress is required for network printers." });

            var printer = new Printer
            {
                Id = Guid.NewGuid(),
                Type = printerType,
                WindowsPrinterName = request.WindowsPrinterName,
                IpAddress = request.IpAddress,
                Port = request.Port > 0 ? request.Port : 9100,
                CopiesPerJob = Math.Max(1, request.Copies),
                IsActive = true,
                IsReceiptPrinter = true,
            };

            var job = new PrintJob
            {
                Id = Guid.NewGuid(),
                PayloadType = PrintPayloadType.Html,
                JobType = PrintJobType.CustomerReceipt,
                PayloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(request.HtmlContent)),
            };

            try
            {
                var success = await _htmlWriter.PrintAsync(job, printer, ct);
                if (success) return Ok(new { success = true });
                return Ok(new { success = false, error = "sidecar reported failure" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LocalPrint] Receipt HTML print failed");
                return Ok(new { success = false, error = ex.Message });
            }
        }

        [HttpOptions("receipt-html")]
        public IActionResult PreflightReceiptHtml()
        {
            // Chrome's Private Network Access preflight needs this header so
            // an HTTPS POS page can call http://127.0.0.1 without being blocked.
            Response.Headers["Access-Control-Allow-Private-Network"] = "true";
            return NoContent();
        }

        private bool IsLoopback()
        {
            var remote = HttpContext.Connection.RemoteIpAddress;
            return remote != null && IPAddress.IsLoopback(remote);
        }
    }

    public sealed class LocalReceiptHtmlRequest
    {
        [Required]
        public string HtmlContent { get; set; } = string.Empty;

        // 0 = Network, 1 = Usb (matches PrinterType enum)
        public int PrinterType { get; set; }

        public string? WindowsPrinterName { get; set; }
        public string? IpAddress { get; set; }
        public int Port { get; set; } = 9100;
        public int Copies { get; set; } = 1;
    }
}
