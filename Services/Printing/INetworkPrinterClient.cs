using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services.Printing
{
    /// <summary>
    /// Sends a raw ESC/POS byte stream to a network thermal printer over TCP.
    /// USB/serial printers are out of scope for direct backend printing — those
    /// require a local agent running on the cashier station and consume jobs
    /// from the same PrintJob queue (future work).
    /// </summary>
    public interface INetworkPrinterClient
    {
        /// <summary>
        /// Throws on transport failure. The caller is responsible for catching
        /// the exception and recording it on the PrintJob row for retry.
        /// </summary>
        Task SendAsync(Printer printer, byte[] payload, CancellationToken ct);
    }
}
