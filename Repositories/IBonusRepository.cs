using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public sealed record BonusListFilter(
        Guid? StaffProfileId,
        string? Status,
        string? BonusType,
        DateOnly? From,
        DateOnly? To,
        int Page,
        int PageSize);

    public sealed record BonusListResult(
        IReadOnlyList<Bonus> Items,
        int Total,
        decimal TotalAmount);

    public interface IBonusRepository
    {
        Task<BonusListResult> GetAsync(BonusListFilter filter, CancellationToken ct);
        Task<Bonus?> GetByIdAsync(Guid id, CancellationToken ct);
        Task<Bonus> AddAsync(Bonus bonus, CancellationToken ct);
        Task UpdateAsync(Bonus bonus, CancellationToken ct);
        Task<int> CountPendingForStaffAsync(Guid staffProfileId, CancellationToken ct);
        Task<IReadOnlyDictionary<Guid, int>> CountPendingByStaffAsync(IReadOnlyList<Guid> staffProfileIds, CancellationToken ct);
        Task<IReadOnlyDictionary<Guid, (int count, decimal total)>> GetTotalsByStaffAsync(IReadOnlyList<Guid> staffProfileIds, CancellationToken ct);
    }
}
