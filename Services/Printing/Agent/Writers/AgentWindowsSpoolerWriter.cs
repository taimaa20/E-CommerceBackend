using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services.Printing.Agent.Writers
{
    /// <summary>
    /// Sends raw ESC/POS bytes through the Windows print spooler in RAW
    /// datatype mode. Used for USB-attached printers (e.g. an XP-80C
    /// connected to USB002 on the cashier PC) that have no Ethernet path.
    ///
    /// Win32 sequence: <c>OpenPrinter → StartDocPrinter(RAW) →
    /// StartPagePrinter → WritePrinter → EndPagePrinter → EndDocPrinter →
    /// ClosePrinter</c>. RAW datatype tells the spooler not to format the
    /// stream — what we send is what the printer prints, byte for byte.
    ///
    /// Windows-only by physical necessity. The class is decorated so the
    /// platform-compat analyzer is satisfied without polluting the entire
    /// project's TFM. Only invoked by <see cref="LocalPrintAgentBackgroundService"/>
    /// when running with <c>PrintAgent:Enabled = true</c>, which is never
    /// the case on Azure App Service.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class AgentWindowsSpoolerWriter : IAgentPrinterWriter
    {
        private readonly ILogger<AgentWindowsSpoolerWriter> _logger;

        public AgentWindowsSpoolerWriter(ILogger<AgentWindowsSpoolerWriter> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task WriteAsync(AgentJobDto job, byte[] payload, CancellationToken ct)
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException(
                    "USB / Windows spooler printing requires Windows. Run the agent on a Windows store PC.");

            if (string.IsNullOrWhiteSpace(job.WindowsPrinterName))
                throw new InvalidOperationException(
                    $"Printer {job.PrinterName} has no WindowsPrinterName configured");

            return WriteRawAsync(
                job.WindowsPrinterName!,
                $"POS ticket {job.OrderNumber}",
                payload,
                job.CopiesPerJob,
                ct);
        }

        public Task WriteRawAsync(string windowsPrinterName, string docName, byte[] payload, int copiesPerJob, CancellationToken ct)
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException(
                    "USB / Windows spooler printing requires Windows. Run the agent on a Windows store PC.");

            if (string.IsNullOrWhiteSpace(windowsPrinterName))
                throw new InvalidOperationException("WindowsPrinterName is required for USB printing.");

            var copies = Math.Max(1, copiesPerJob);
            for (var i = 0; i < copies; i++)
            {
                ct.ThrowIfCancellationRequested();
                SendRaw(windowsPrinterName, docName, payload);
            }

            _logger.LogInformation(
                "Agent spooler → {Printer} ({Bytes}b x{Copies})",
                windowsPrinterName, payload.Length, copies);

            return Task.CompletedTask;
        }

        private static void SendRaw(string printerName, string docName, byte[] bytes)
        {
            if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
                ThrowLastError("OpenPrinter");

            try
            {
                var di = new DOCINFOA { pDocName = docName, pOutputFile = null!, pDataType = "RAW" };
                if (StartDocPrinter(hPrinter, 1, di) == 0) ThrowLastError("StartDocPrinter");
                try
                {
                    if (!StartPagePrinter(hPrinter)) ThrowLastError("StartPagePrinter");
                    try
                    {
                        var unmanaged = Marshal.AllocHGlobal(bytes.Length);
                        try
                        {
                            Marshal.Copy(bytes, 0, unmanaged, bytes.Length);
                            if (!WritePrinter(hPrinter, unmanaged, bytes.Length, out var written))
                                ThrowLastError("WritePrinter");
                            if (written != bytes.Length)
                                throw new IOException($"Spooler wrote {written}/{bytes.Length} bytes");
                        }
                        finally { Marshal.FreeHGlobal(unmanaged); }
                    }
                    finally { EndPagePrinter(hPrinter); }
                }
                finally { EndDocPrinter(hPrinter); }
            }
            finally { ClosePrinter(hPrinter); }
        }

        private static void ThrowLastError(string op)
        {
            var code = Marshal.GetLastWin32Error();
            throw new System.ComponentModel.Win32Exception(code, $"{op} failed (Win32 {code})");
        }

        // ── PInvoke ──────────────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string pDocName = string.Empty;
            [MarshalAs(UnmanagedType.LPStr)] public string pOutputFile = string.Empty;
            [MarshalAs(UnmanagedType.LPStr)] public string pDataType = string.Empty;
        }

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Ansi, EntryPoint = "OpenPrinterA")]
        private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Ansi, EntryPoint = "StartDocPrinterA")]
        private static extern int StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);
    }
}
