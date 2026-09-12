using Microsoft.Extensions.Logging;

namespace RestaurantPos.Api.Modules.Dashboard.Services
{
    /// <summary>
    /// Tiny helper for calculator services: run a calculator, log any
    /// exception with structured context, and return a safe default. A bad
    /// query in one calculator must never take down the whole dashboard
    /// snapshot — the user sees the rest of the page populated and the
    /// failing widget shows "—" instead of the whole page 500'ing.
    /// </summary>
    public static class SafeAsync
    {
        public static async Task<T> RunAsync<T>(
            ILogger logger,
            string calculator,
            Func<Task<T>> work,
            T fallback)
        {
            try
            {
                return await work();
            }
            catch (OperationCanceledException)
            {
                throw; // genuine cancellation should propagate
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Dashboard calculator '{Calculator}' failed; returning fallback.", calculator);
                return fallback;
            }
        }
    }
}
