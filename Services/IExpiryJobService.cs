using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    public interface IExpiryJobService
    {
        Task<ExpiryJobRunResult> RunAsync(CancellationToken cancellationToken = default);
    }

    public sealed class ExpiryJobRunResult
    {
        public string Message { get; init; } = string.Empty;
        public int ExpiredLogged { get; init; }
        public int NearExpiryNotified { get; init; }
        public bool SkippedBecauseAlreadyRunning { get; init; }
    }
}
