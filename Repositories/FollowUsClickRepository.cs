using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public class FollowUsClickRepository : IFollowUsClickRepository
    {
        private readonly PosDbContext _context;

        public FollowUsClickRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task AddAsync(FollowUsClick click, CancellationToken ct)
        {
            _context.FollowUsClicks.Add(click);
            await _context.SaveChangesAsync(ct);
        }
    }
}
