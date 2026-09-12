using System.Net.Sockets;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services.Printing
{
    public class NetworkPrinterClient : INetworkPrinterClient
    {
        // Short connect timeout — production printers should answer in <1s on a
        // healthy LAN. A long hang here means the printer is offline; we want
        // the queue to register a failure quickly and retry.
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(15);

        // Chunked-write tuning. Xprinter clones can drop trailing bytes on one
        // large write; 1KB chunks keep enough pacing without making raster
        // kitchen tickets crawl.
        private const int ChunkSize = 1024;
        private static readonly TimeSpan ChunkDelay = TimeSpan.FromMilliseconds(12);

        // After the last byte is sent we hold the TCP connection open for
        // this long before letting `using` close it. Some Xprinter firmwares
        // abort the in-progress job when they see TCP FIN, dropping anything
        // still in the buffer — and the cut tail (GS V 0) is the last thing
        // they receive. Holding the connection until the printer has had
        // time to physically print + cut prevents the abort.
        private static readonly TimeSpan PostWriteHold = TimeSpan.FromSeconds(1);

        private readonly ILogger<NetworkPrinterClient> _logger;

        public NetworkPrinterClient(ILogger<NetworkPrinterClient> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendAsync(Printer printer, byte[] payload, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(printer);
            ArgumentNullException.ThrowIfNull(payload);

            if (printer.Type != PrinterType.Network)
                throw new InvalidOperationException($"Printer {printer.Id} is not a network printer.");
            if (string.IsNullOrWhiteSpace(printer.IpAddress))
                throw new InvalidOperationException($"Printer {printer.Id} has no IP address configured.");

            using var client = new TcpClient { NoDelay = true, SendTimeout = (int)WriteTimeout.TotalMilliseconds };

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(ConnectTimeout);

            try
            {
                await client.ConnectAsync(printer.IpAddress, printer.Port, connectCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException($"Connect to {printer.IpAddress}:{printer.Port} timed out after {ConnectTimeout.TotalSeconds}s.");
            }

            using var stream = client.GetStream();
            stream.WriteTimeout = (int)WriteTimeout.TotalMilliseconds;

            var copies = Math.Max(1, printer.CopiesPerJob);
            for (var i = 0; i < copies; i++)
            {
                await WriteChunkedAsync(stream, payload, ct);
            }

            // Hold the connection open so the printer can finish printing
            // + cutting BEFORE TCP FIN reaches it.
            try { await Task.Delay(PostWriteHold, ct); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }

            _logger.LogInformation(
                "Printed {Bytes} bytes x{Copies} to {Printer} @ {Ip}:{Port}",
                payload.Length, copies, printer.Name, printer.IpAddress, printer.Port);
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
