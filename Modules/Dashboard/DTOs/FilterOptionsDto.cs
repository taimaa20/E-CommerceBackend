namespace RestaurantPos.Api.Modules.Dashboard.DTOs
{
    /// <summary>Lightweight option lists used to populate the filter bar.</summary>
    public sealed record DashboardFilterOptionsDto(
        IReadOnlyList<EmployeeOptionDto> Cashiers,
        IReadOnlyList<EmployeeOptionDto> Waiters,
        IReadOnlyList<CategoryOptionDto> Categories,
        IReadOnlyList<string>            PaymentMethods)
    {
        public IReadOnlyList<PaymentMethodOptionDto> PaymentMethodOptions { get; init; } =
            Array.Empty<PaymentMethodOptionDto>();
        public IReadOnlyList<PartnerOptionDto> Partners { get; init; } =
            Array.Empty<PartnerOptionDto>();
        public IReadOnlyList<WarehouseOptionDto> Warehouses { get; init; } =
            Array.Empty<WarehouseOptionDto>();
    }

    public sealed record WarehouseOptionDto(Guid Id, string Name, string? NameAr, bool IsActive);

    public sealed record EmployeeOptionDto(Guid Id, string Name, string? NameAr = null);
    public sealed record CategoryOptionDto(Guid Id, string Name, string? NameAr);
    public sealed record PaymentMethodOptionDto(
        string Value,
        string Name,
        string? NameAr,
        bool IsActive);
    public sealed record PartnerOptionDto(
        string Value,
        string Name,
        string? NameAr,
        bool IsActive);
}
