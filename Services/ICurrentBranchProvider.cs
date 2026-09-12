namespace RestaurantPos.Api.Services
{
    public interface ICurrentBranchProvider
    {
        Guid? GetSelectedBranchId();
    }
}
