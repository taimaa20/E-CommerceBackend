namespace RestaurantPos.Api.Modules.Dashboard.DTOs.Common
{
    /// <summary>
    /// Generic envelope for a single KPI value plus its optional comparison
    /// delta. Used by every dashboard so the frontend KpiCard component can
    /// render the same shape everywhere.
    /// </summary>
    public sealed record KpiValueDto<T>(T Value, T? PreviousValue = default, decimal? ChangePercent = null);

    public sealed record TimeSeriesPointDto(DateTime BucketStartUtc, decimal Value, int? Count = null);

    public sealed record CategorySliceDto(string Label, string? LabelAr, decimal Value, decimal? Percentage = null, int? Count = null);

    public sealed record RankedRowDto(
        Guid? Id,
        string Label,
        string? LabelAr,
        decimal PrimaryValue,
        decimal? SecondaryValue = null,
        int? Count = null);

    public sealed record AlertDto(
        string Code,
        string Severity,        // "info" | "warning" | "danger"
        string TitleKey,        // i18n key — the frontend translates
        string? Message = null,
        object? Payload = null);
}
