using System.Net.Sockets;
using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services.Printing.Agent.Writers
{
    /// <summary>
    /// Mirror of <see cref="NetworkPrinterClient"/> — but invoked by the
    /// in-store agent where private LAN IPs (192.168.x.x) are reachable.
    /// Same TCP/IP-9100 ESC/POS path the cloud uses for non-LAN printers.
    /// </summary>
    public sealed class AgentNetworkWriter : IAgentPrinterWriter
    {
        // TEMPORARY DIAGNOSTIC: 60s connect timeout (was 10s, originally 3s).
        // Goal: prove whether the kitchen network printer is reachable from
        // the backend at all, by removing timeout-tuning as a variable.
        // Any healthy LAN printer answers in <1s; this only changes behavior
        // for pathological cases. REVERT to 10s once the printer is stable.
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(30);

        // Xprinter / ESC/POS clones can drop trailing bytes on one large write.
        // 1KB chunks keep the printer fed without turning raster kitchen tickets
        // into 20+ second jobs.
        private const int ChunkSize = 1024;
        private static readonly TimeSpan ChunkDelay = TimeSpan.FromMilliseconds(12);
        private static readonly TimeSpan PostWriteHold = TimeSpan.FromSeconds(1);

        private readonly ILogger<AgentNetworkWriter> _logger;

        public AgentNetworkWriter(ILogger<AgentNetworkWriter> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task WriteAsync(AgentJobDto job, byte[] payload, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(job.IpAddress))
                throw new InvalidOperationException($"Printer {job.PrinterName} has no IP address");

            using var client = new TcpClient { NoDelay = true, SendTimeout = (int)WriteTimeout.TotalMilliseconds };
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(ConnectTimeout);

            try
            {
                await client.ConnectAsync(job.IpAddress, job.Port, connectCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Connect to {job.IpAddress}:{job.Port} timed out after {ConnectTimeout.TotalSeconds}s");
            }

            using var stream = client.GetStream();
            stream.WriteTimeout = (int)WriteTimeout.TotalMilliseconds;

            var copies = Math.Max(1, job.CopiesPerJob);
            for (var i = 0; i < copies; i++)
            {
                await WriteChunkedAsync(stream, payload, ct);
            }

            try { await Task.Delay(PostWriteHold, ct); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }

            _logger.LogInformation(
                "Agent TCP → {Printer} ({Ip}:{Port}) {Bytes}b x{Copies}",
                job.PrinterName, job.IpAddress, job.Port, payload.Length, copies);
        }

        private static async Task WriteChunkedAsync(NetworkStream stream, byte[] payload, CancellationToken ct)
        {
            for (var offset = 0; offset < payload.Length; offset += ChunkSize)
            {
                var len = Math.Min(ChunkSize, payload.Length - offset);
                await stream.WriteAsync(payload.AsMemory(offset, len), ct);
                await stream.FlushAsync(ct);
                if (offset + len < payload.Length)
                    await Task.Delay(ChunkDelay, ct);
            }
        }
    }
}
