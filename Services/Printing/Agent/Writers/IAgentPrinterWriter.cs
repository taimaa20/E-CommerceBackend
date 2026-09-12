using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services.Printing.Agent.Writers
{
    /// <summary>
    /// Sends a single job's ESC/POS payload to a physical printer.
    /// Implemented by <see cref="AgentNetworkWriter"/> (TCP) and
    /// <see cref="AgentWindowsSpoolerWriter"/> (USB / Windows spooler).
    /// </summary>
    public interface IAgentPrinterWriter
    {
        /// <summary>Throws on failure. Caller catches and reports back to the cloud.</summary>
        Task WriteAsync(AgentJobDto job, byte[] payload, CancellationToken ct);
    }
}
