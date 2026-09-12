using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services;

/// <summary>
/// The public store's view of the existing bundle-offer engine.
///
/// It adds no pricing rule of its own: the offer set, the schedule and the per-line
/// allocation are the ones order intake already applies, so an offer shown here is an
/// offer the order will honour at the same price.
/// </summary>
public interface IStorefrontOfferService
{
    /// <summary>Offers that are live for this branch right now, newest first.</summary>
    /// <param name="productId">When given, only offers containing that product.</param>
    Task<OnlineStoreOffersDto> GetLiveOffersAsync(
        Guid tenantId,
        Guid? selectedBranchId,
        Guid? productId,
        CancellationToken ct);

    /// <summary>
    /// Throws unless every offer line may be sold by this branch right now AND the lines form
    /// whole copies of the bundle. Without the second check a shopper could drop one item from
    /// a bundle and keep the rest at bundle prices.
    /// </summary>
    Task ValidateOfferLinesAsync(
        Guid tenantId,
        Guid? branchId,
        IReadOnlyList<OfferOrderLine> lines,
        CancellationToken ct);
}

/// <summary>One cart line that claims to belong to a bundle offer.</summary>
public sealed record OfferOrderLine(int OfferId, Guid ProductId, int Quantity);
