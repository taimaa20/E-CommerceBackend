namespace RestaurantPos.Api.DTOs.Hr
{
    /// Single filter shape shared by leave / loan / permission admin list endpoints.
    /// Date semantics differ per request kind — services interpret From/To against
    /// their own primary date column (start/request/permission date respectively).
    public sealed record HrRequestAdminFilter(
        Guid? EmployeeId,
        string? Status,
        string? TypeCode,
        DateOnly? From,
        DateOnly? To,
        int Page,
        int PageSize)
    {
        public int NormalizedPage => Page < 1 ? 1 : Page;
        public int NormalizedPageSize => PageSize < 1 ? 25 : Math.Min(PageSize, 200);
        public int Skip => (NormalizedPage - 1) * NormalizedPageSize;
    }
}
