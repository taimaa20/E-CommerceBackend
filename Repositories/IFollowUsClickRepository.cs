using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public interface IFollowUsClickRepository
    {
        Task AddAsync(FollowUsClick click, CancellationToken ct);
    }
}
