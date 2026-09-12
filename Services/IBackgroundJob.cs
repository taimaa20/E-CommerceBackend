using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    public interface IBackgroundJob
    {
        string JobName { get; }
        Task RunIfDueAsync(CancellationToken cancellationToken);
    }
}
