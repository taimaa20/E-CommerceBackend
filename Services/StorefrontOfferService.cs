using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Services.Time;

namespace RestaurantPos.Api.Services;

public sealed class StorefrontOfferService : IStorefrontOfferService
{
    /// A window covering the whole day carries no useful "available between" line.
    private static readonly TimeOnly DayStart = new(0, 0);
    private static readonly TimeOnly DayEnd = new(23, 59);

    private readonly IStorefrontCatalogRepository _repository;
    private readonly IBranchConfigurationService _branchConfigurationService;
    private readonly ISettingsService _settingsService;

    public StorefrontOfferService(
        IStorefrontCatalogRepository repository,
        IBranchConfigurationService branchConfigurationService,
        ISettingsService settingsService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _branchConfigurationService = branchConfigurationService ?? throw new ArgumentNullException(nameof(branchConfigurationService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    public async Task<OnlineStoreOffersDto> GetLiveOffersAsync(
        Guid tenantId,
        Guid? selectedBranchId,
        Guid? productId,
        CancellationToken ct)
    {
        var live = await LoadLiveOffersAsync(tenantId, selectedBranchId, ct);

        var offers = live
            .Where(offer => !productId.HasValue || offer.Products.Any(row => row.ProductId == productId.Value))
            .Select(Map)
            .Where(offer => offer is not null)
            .Select(offer => offer!)
            .OrderByDescending(offer => offer.SavingPercent)
            .ThenByDescending(offer => offer.Id)
            .ToList();

        return new OnlineStoreOffersDto { Offers = offers };
    }

    public async Task ValidateOfferLinesAsync(
        Guid tenantId,
        Guid? branchId,
        IReadOnlyList<OfferOrderLine> lines,
        CancellationToken ct)
    {
        if (lines.Count == 0) return;

        var live = (await LoadLiveOffersAsync(tenantId, branchId, ct)).ToDictionary(offer => offer.Id);

        foreach (var group in lines.GroupBy(line => line.OfferId))
        {
            if (!live.TryGetValue(group.Key, out var offer))
                throw new ValidationException("One of the selected offers is no longer available. Refresh the store and try again.");
            if (!IsWholeBundle(offer, group.ToList()))
                throw new ValidationException($"\"{offer.Name}\" must be ordered as a complete offer. Add the offer again to fix the items.");
        }
    }

    /// <summary>
    /// True when the submitted lines are exactly N complete copies of the bundle: the same
    /// products, no extras, and every quantity the offer's quantity times the same N.
    /// </summary>
    private static bool IsWholeBundle(StorefrontOfferRow offer, IReadOnlyList<OfferOrderLine> lines)
    {
        var submitted = lines
            .GroupBy(line => line.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));

        var required = offer.Products
            .GroupBy(row => row.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.Quantity));

        if (submitted.Count != required.Count || required.Values.Any(quantity => quantity <= 0)) return false;

        int? copies = null;
        foreach (var (productId, requiredQuantity) in required)
        {
            if (!submitted.TryGetValue(productId, out var submittedQuantity)) return false;
            if (submittedQuantity <= 0 || submittedQuantity % requiredQuantity != 0) return false;

            var lineCopies = submittedQuantity / requiredQuantity;
            copies ??= lineCopies;
            if (copies != lineCopies) return false;
        }

        return copies is > 0;
    }

    // -- Composition ---------------------------------------------------------

    /// <summary>
    /// The one definition of "live": enabled for this branch, active, and inside its date and
    /// daily-time window measured on the restaurant's own clock. Both the read used for
    /// display and the check used at order intake go through here, so they cannot disagree.
    /// </summary>
    private async Task<IReadOnlyList<StorefrontOfferRow>> LoadLiveOffersAsync(
        Guid tenantId,
        Guid? selectedBranchId,
        CancellationToken ct)
    {
        var branchId = await _repository.ResolveStoreBranchIdAsync(tenantId, selectedBranchId, ct);
        if (!branchId.HasValue) return [];

        var enabledOfferIds = await _branchConfigurationService.GetEnabledOfferIdsAsync(branchId.Value, ct);
        if (enabledOfferIds.Count == 0) return [];

        var offers = await _repository.GetOffersAsync(enabledOfferIds.ToList(), ct);
        if (offers.Count == 0) return [];

        var localNow = await GetRestaurantLocalNowAsync(tenantId, ct);
        return offers
            .Where(offer => AvailabilityHelper.IsAvailableAt(
                offer.StartDate,
                offer.EndDate,
                offer.StartTime,
                offer.EndTime,
                localNow))
            .ToList();
    }

    private async Task<DateTime> GetRestaurantLocalNowAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await _settingsService.GetSettingsAsync(tenantId, ct);
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, RestaurantTimeZone.Resolve(settings.TimeZoneId));
    }

    /// <summary>
    /// Turns one live offer into its customer-facing shape. Returns null when the offer has
    /// nothing sellable left in it or would show a saving the numbers do not support — an
    /// offer that is not genuinely cheaper is not presented as a deal.
    /// </summary>
    private static OnlineStoreOfferDto? Map(StorefrontOfferRow offer)
    {
        var products = offer.Products
            .Where(row => row.IsProductActive && row.Quantity > 0 && !row.RequiresChoice)
            .ToList();
        // A bundle is all-or-nothing: if any part of it cannot be sold online as-is, the whole
        // offer stays off the storefront rather than being shown at a price it cannot honour.
        if (products.Count != offer.Products.Count || products.Count == 0) return null;

        var originalPrice = products.Sum(row => row.ListUnitPrice * row.Quantity);
        if (originalPrice <= 0 || offer.FinalPrice >= originalPrice) return null;

        var allocation = OfferPricingHelper
            .Allocate(
                products.Select(row => new OfferAllocationLine(row.ProductId, row.Quantity, row.ListUnitPrice)).ToList(),
                offer.FinalPrice)
            .ToDictionary(line => line.ProductId, line => line.UnitPrice);

        var saving = originalPrice - offer.FinalPrice;

        return new OnlineStoreOfferDto
        {
            Id = offer.Id,
            Name = offer.Name,
            NameAr = string.IsNullOrWhiteSpace(offer.NameAr) ? null : offer.NameAr,
            Description = offer.Description,
            DescriptionAr = offer.DescriptionAr,
            // A data: URL is a legacy inline image that would bloat every storefront payload.
            ImageUrl = offer.ImageUrl?.StartsWith("data:", StringComparison.OrdinalIgnoreCase) == true
                ? null
                : offer.ImageUrl,
            Price = offer.FinalPrice,
            OriginalPrice = originalPrice,
            SavingAmount = saving,
            SavingPercent = Math.Max(1, (int)Math.Round(saving / originalPrice * 100m, MidpointRounding.AwayFromZero)),
            EndsOn = offer.EndDate,
            DailyStartTime = FormatWindow(offer.StartTime, offer.EndTime, offer.StartTime),
            DailyEndTime = FormatWindow(offer.StartTime, offer.EndTime, offer.EndTime),
            Items = products.Select(row => new OnlineStoreOfferItemDto
            {
                ProductId = row.ProductId,
                CategoryId = row.CategoryId,
                Name = row.Name,
                NameAr = row.NameAr,
                ImageUrl = row.ImageUrl,
                ImageKey = row.ImageKey,
                Quantity = row.Quantity,
                UnitPrice = allocation.TryGetValue(row.ProductId, out var unitPrice) ? unitPrice : row.ListUnitPrice,
                ListUnitPrice = row.ListUnitPrice,
            }).ToList(),
        };
    }

    private static string? FormatWindow(TimeOnly start, TimeOnly end, TimeOnly value)
        => start <= DayStart && end >= DayEnd ? null : value.ToString("HH\\:mm");
}
