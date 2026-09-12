using System.Security.Claims;

namespace RestaurantPos.Api.Interfaces
{
    public interface ICurrentUserResolver
    {
        Task<Guid?> ResolveUserIdAsync(ClaimsPrincipal principal);
    }
}
