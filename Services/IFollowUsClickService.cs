using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public interface IFollowUsClickService
    {
        Task TrackClickAsync(FollowUsClickCreateDto dto, string? userAgent, string? referrer, CancellationToken ct);
    }
}
