namespace RestaurantPos.Api.Hubs
{
    public static class KitchenHubGroups
    {
        public static string Branch(Guid branchId) => $"branch:{branchId:N}";
    }
}
