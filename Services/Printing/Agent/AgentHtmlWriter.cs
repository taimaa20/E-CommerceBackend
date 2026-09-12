using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services.Printing.Agent.Writers;

namespace RestaurantPos.Api.Services.Printing.Agent
{
    public sealed class AgentHtmlWriter
    {
        private const string EscPosRasterMode = "escpos-raster";
        private const string RenderEscPosRasterMode = "render-escpos-raster";

        private static readonly TimeSpan SidecarTimeout = TimeSpan.FromSeconds(30);
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly string _sidecarPath;
        private readonly string _nodeExe;
        private readonly AgentWindowsSpoolerWriter _spooler;
        private readonly ILogger<AgentHtmlWriter> _logger;

        public AgentHtmlWriter(
            IConfiguration configuration,
            AgentWindowsSpoolerWriter spooler,
            ILogger<AgentHtmlWriter> logger)
        {
            _spooler = spooler ?? throw new ArgumentNullException(nameof(spooler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sidecarPath = configuration["PrintAgent:SidecarPath"]
                ?? Path.Combine(AppContext.BaseDirectory, "print-agent-sidecar", "index.js");
            // LocalSystem services don't always inherit the user's PATH, so allow
            // an explicit node.exe path via PrintAgent:NodeExe. Defaults to "node".
            _nodeExe = configuration["PrintAgent:NodeExe"] ?? "node";
        }

        public async Task<bool> PrintAsync(AgentJobDto job, byte[] payloadBytes, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(job);
            ArgumentNullException.ThrowIfNull(payloadBytes);

            if (job.PayloadType != (int)PrintPayloadType.Html)
                throw new InvalidOperationException("Wrong payload type routed to AgentHtmlWriter");

            var html = Encoding.UTF8.GetString(payloadBytes);
            if ((PrinterType)job.PrinterType == PrinterType.Usb)
                return job.JobType == (int)PrintJobType.CustomerReceipt
                    ? await PrintUsbPdfAsync(job, html, ct)
                    : await PrintUsbRasterAsync(job, html, ct);

            if (!TryBuildRequest(job, html, out var request))
                return false;

            return await RunSidecarAsync(job.Id, request, ct);
        }

        public async Task<bool> PrintAsync(PrintJob job, Printer printer, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(job);
            ArgumentNullException.ThrowIfNull(printer);

            if (job.PayloadType != PrintPayloadType.Html)
                throw new InvalidOperationException("Wrong payload type routed to AgentHtmlWriter");

            var html = Encoding.UTF8.GetString(Convert.FromBase64String(job.PayloadBase64));
            if (printer.Type == PrinterType.Usb)
                return job.JobType == PrintJobType.CustomerReceipt
                    ? await PrintUsbPdfAsync(job, printer, html, ct)
                    : await PrintUsbRasterAsync(job, printer, html, ct);

            if (!TryBuildRequest(job, printer, html, out var request))
                return false;

            return await RunSidecarAsync(job.Id, request, ct);
        }

        private bool TryBuildRequest(AgentJobDto job, string html, out SidecarPrintRequest request)
        {
            request = default!;
            var printerType = (PrinterType)job.PrinterType;

            if (printerType == PrinterType.Network)
            {
                if (string.IsNullOrWhiteSpace(job.IpAddress))
                {
                    _logger.LogError("Network printer {PrinterId} has no IpAddress configured", job.PrinterId);
                    return false;
                }

                request = SidecarPrintRequest.Network(html, job.IpAddress!, job.Port, job.CopiesPerJob, EscPosRasterMode);
                return true;
            }

            throw new InvalidOperationException($"Unknown printer type {job.PrinterType}");
        }

        private bool TryBuildRequest(PrintJob job, Printer printer, string html, out SidecarPrintRequest request)
        {
            request = default!;

            if (printer.Type == PrinterType.Network)
            {
                if (string.IsNullOrWhiteSpace(printer.IpAddress))
                {
                    _logger.LogError("Network printer {PrinterId} has no IpAddress configured", printer.Id);
                    return false;
                }

                request = SidecarPrintRequest.Network(html, printer.IpAddress!, printer.Port, printer.CopiesPerJob, EscPosRasterMode);
                return true;
            }

            throw new InvalidOperationException($"Unknown printer type {printer.Type} for job {job.Id}");
        }

        private async Task<bool> PrintUsbPdfAsync(AgentJobDto job, string html, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(job.WindowsPrinterName))
            {
                _logger.LogError("USB printer {PrinterId} has no WindowsPrinterName configured", job.PrinterId);
                return false;
            }

            var request = SidecarPrintRequest.Usb(html, job.WindowsPrinterName!, job.CopiesPerJob);
            return await RunSidecarAsync(job.Id, request, ct);
        }

        private async Task<bool> PrintUsbPdfAsync(PrintJob job, Printer printer, string html, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(printer.WindowsPrinterName))
            {
                _logger.LogError("USB printer {PrinterId} has no WindowsPrinterName configured", printer.Id);
                return false;
            }

            var request = SidecarPrintRequest.Usb(html, printer.WindowsPrinterName!, printer.CopiesPerJob);
            return await RunSidecarAsync(job.Id, request, ct);
        }

        private async Task<bool> PrintUsbRasterAsync(AgentJobDto job, string html, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(job.WindowsPrinterName))
            {
                _logger.LogError("USB printer {PrinterId} has no WindowsPrinterName configured", job.PrinterId);
                return false;
            }

            var payload = await RenderEscPosRasterAsync(job.Id, html, ct);
            if (payload == null) return false;
            if (!OperatingSystem.IsWindows())
            {
                _logger.LogError("USB HTML printing requires the print agent to run on Windows.");
                return false;
            }

            await _spooler.WriteRawAsync(
                job.WindowsPrinterName!,
                $"POS ticket {job.OrderNumber}",
                payload,
                job.CopiesPerJob,
                ct);
            return true;
        }

        private async Task<bool> PrintUsbRasterAsync(PrintJob job, Printer printer, string html, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(printer.WindowsPrinterName))
            {
                _logger.LogError("USB printer {PrinterId} has no WindowsPrinterName configured", printer.Id);
                return false;
            }

            var payload = await RenderEscPosRasterAsync(job.Id, html, ct);
            if (payload == null) return false;
            if (!OperatingSystem.IsWindows())
            {
                _logger.LogError("USB HTML printing requires the print agent to run on Windows.");
                return false;
            }

            await _spooler.WriteRawAsync(
                printer.WindowsPrinterName!,
                $"POS ticket {job.OrderNumberSnapshot}",
                payload,
                printer.CopiesPerJob,
                ct);
            return true;
        }

        private async Task<byte[]?> RenderEscPosRasterAsync(Guid jobId, string html, CancellationToken ct)
        {
            var request = SidecarPrintRequest.RenderEscPosRaster(html);
            var result = await RunSidecarForResultAsync(jobId, request, ct);
            if (result == null) return null;

            if (!result.Success)
            {
                _logger.LogError("Print sidecar failed for job {JobId}: {Error}", jobId, result.Error);
                return null;
            }

            if (string.IsNullOrWhiteSpace(result.PayloadBase64))
            {
                _logger.LogError("Print sidecar returned no raster payload for job {JobId}", jobId);
                return null;
            }

            try
            {
                return Convert.FromBase64String(result.PayloadBase64);
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Print sidecar returned invalid raster payload for job {JobId}", jobId);
                return null;
            }
        }

        private async Task<bool> RunSidecarAsync(Guid jobId, SidecarPrintRequest request, CancellationToken ct)
        {
            var result = await RunSidecarForResultAsync(jobId, request, ct);
            if (result == null) return false;

            if (result.Success)
                return true;

            _logger.LogError("Print sidecar failed for job {JobId}: {Error}", jobId, result.Error);
            return false;
        }

        private async Task<SidecarPrintResult?> RunSidecarForResultAsync(Guid jobId, SidecarPrintRequest request, CancellationToken ct)
        {
            if (!File.Exists(_sidecarPath))
            {
                _logger.LogError("Print sidecar not found at {SidecarPath}", _sidecarPath);
                return null;
            }

            Process process;
            try
            {
                process = StartSidecarProcess();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to spawn print sidecar (node='{Node}', script='{Script}'). " +
                    "On Windows services this usually means node.exe is not on the System PATH. " +
                    "Set PrintAgent:NodeExe to the full path of node.exe.",
                    _nodeExe, _sidecarPath);
                return null;
            }
            using var _ = process;
            var input = JsonSerializer.Serialize(request, Json);

            await process.StandardInput.WriteAsync(input.AsMemory(), ct);
            process.StandardInput.Close();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            if (!await WaitForExitAsync(process, ct))
            {
                _logger.LogError("Print sidecar timed out for job {JobId}", jobId);
                return null;
            }

            var stderr = await stderrTask;
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                _logger.LogWarning("Print sidecar stderr for job {JobId}: {Stderr}", jobId, stderr.Trim());
            }

            var stdout = (await stdoutTask).Trim();
            if (!TryReadResult(stdout, out var result))
            {
                _logger.LogError("Print sidecar returned invalid output for job {JobId}: {Stdout}", jobId, stdout);
                return null;
            }

            return result;
        }

        private Process StartSidecarProcess()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _nodeExe,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_sidecarPath) ?? AppContext.BaseDirectory
            };
            startInfo.ArgumentList.Add(_sidecarPath);

            return Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start print sidecar process.");
        }

        private static async Task<bool> WaitForExitAsync(Process process, CancellationToken ct)
        {
            using var timeout = new CancellationTokenSource(SidecarTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            try
            {
                await process.WaitForExitAsync(linked.Token);
                return true;
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                TryKill(process);
                return false;
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort only; timeout is already logged by the caller.
            }
        }

        private static bool TryReadResult(string stdout, out SidecarPrintResult result)
        {
            result = default!;
            if (string.IsNullOrWhiteSpace(stdout)) return false;

            try
            {
                result = JsonSerializer.Deserialize<SidecarPrintResult>(stdout, Json)!;
                return result != null;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private sealed record SidecarPrintRequest(
            string Html,
            string Type,
            string? PrinterName,
            string? PrinterIp,
            int PrinterPort,
            int Copies,
            string Mode)
        {
            public static SidecarPrintRequest Network(string html, string printerIp, int printerPort, int copies, string mode)
                => new(html, "network", null, printerIp, printerPort > 0 ? printerPort : 9100, Math.Max(1, copies), mode);

            public static SidecarPrintRequest Usb(string html, string printerName, int copies)
                => new(html, "usb", printerName, null, 9100, Math.Max(1, copies), "pdf");

            public static SidecarPrintRequest RenderEscPosRaster(string html)
                => new(html, "render", null, null, 9100, 1, RenderEscPosRasterMode);
        }

        private sealed class SidecarPrintResult
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
            public string? PayloadBase64 { get; set; }
        }
    }
}
