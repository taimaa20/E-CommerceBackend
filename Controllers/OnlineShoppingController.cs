using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Payments.Application.CreatePayment;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("api")]
[Route("api/online-shopping")]
public sealed class OnlineShoppingController : ControllerBase
{
    private const int DefaultStorePageSize = 12;
    private const int MaxStorePageSize = 48;
    private const int MaxSearchLength = 100;
    /// A favourites list is a shopper's own shortlist, not a bulk export route.
    private const int MaxProductSetIds = 200;

    private readonly IOnlineShoppingService _service;
    private readonly IOnlineStoreCatalogService _catalogService;
    private readonly IOnlineShoppingCheckoutService _checkoutService;
    private readonly IStorefrontOfferService _offerService;
    private readonly IStorefrontBannerService _bannerService;
    private readonly IPublicReceiptService _receiptService;
    private readonly IWhatsAppContactService _whatsAppContactService;
    private readonly ITenantResolver _tenantResolver;
    private readonly ICurrentBranchProvider _branchProvider;

    public OnlineShoppingController(
        IOnlineShoppingService service,
        IOnlineStoreCatalogService catalogService,
        IOnlineShoppingCheckoutService checkoutService,
        IStorefrontOfferService offerService,
        IStorefrontBannerService bannerService,
        IPublicReceiptService receiptService,
        IWhatsAppContactService whatsAppContactService,
        ITenantResolver tenantResolver,
        ICurrentBranchProvider branchProvider)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _checkoutService = checkoutService ?? throw new ArgumentNullException(nameof(checkoutService));
        _offerService = offerService ?? throw new ArgumentNullException(nameof(offerService));
        _bannerService = bannerService ?? throw new ArgumentNullException(nameof(bannerService));
        _receiptService = receiptService ?? throw new ArgumentNullException(nameof(receiptService));
        _whatsAppContactService = whatsAppContactService ?? throw new ArgumentNullException(nameof(whatsAppContactService));
        _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
        _branchProvider = branchProvider ?? throw new ArgumentNullException(nameof(branchProvider));
    }

    [HttpGet("context")]
    [ProducesResponseType(typeof(OnlineMenuOrderContextDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrderContext(CancellationToken ct)
        => Ok(await _service.GetOrderContextAsync(
            _tenantResolver.GetTenantId(),
            _branchProvider.GetSelectedBranchId(),
            ct));

    [HttpGet("catalog")]
    [ProducesResponseType(typeof(OnlineStoreCatalogDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCatalog(
        [FromQuery] Guid? categoryId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultStorePageSize,
        [FromQuery] OnlineStoreCatalogSort sort = OnlineStoreCatalogSort.Featured,
        CancellationToken ct = default)
    {
        if (page < 1)
            return BadRequest(new { message = "Page must be greater than zero." });
        if (pageSize < 1 || pageSize > MaxStorePageSize)
            return BadRequest(new { message = $"Page size must be between 1 and {MaxStorePageSize}." });
        if (!Enum.IsDefined(sort))
            return BadRequest(new { message = "Unknown sort option." });

        var query = new OnlineStoreCatalogQuery
        {
            CategoryId = categoryId,
            Search = search,
            Page = page,
            PageSize = pageSize,
            Sort = sort
        };
        return Ok(await _catalogService.GetCatalogAsync(
            _tenantResolver.GetTenantId(),
            _branchProvider.GetSelectedBranchId(),
            GeneralHelper.IsArabicRequested(Request),
            query,
            ct));
    }

    /// Merchandising payload for the storefront home. One request instead of the store
    /// pulling several catalogue pages to assemble its discovery rows.
    [HttpGet("storefront")]
    [ProducesResponseType(typeof(OnlineStoreHomeDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStorefrontHome(CancellationToken ct)
        => Ok(await _catalogService.GetHomeAsync(
            _tenantResolver.GetTenantId(),
            _branchProvider.GetSelectedBranchId(),
            GeneralHelper.IsArabicRequested(Request),
            ct));

    /// The catalogue rows for an explicit set of product ids. The storefront's favourites are
    /// held on the shopper's own device as ids only, so price, stock and imagery still come
    /// from here — a favourite can never show a price the catalogue no longer charges.
    [HttpGet("catalog/products")]
    [ProducesResponseType(typeof(OnlineStoreProductSetDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProductSet([FromQuery] string? ids, CancellationToken ct)
    {
        var productIds = ParseProductIds(ids);
        if (productIds.Count > MaxProductSetIds)
            return BadRequest(new { message = $"At most {MaxProductSetIds} products can be requested at once." });

        return Ok(await _catalogService.GetProductSetAsync(
            _tenantResolver.GetTenantId(),
            _branchProvider.GetSelectedBranchId(),
            GeneralHelper.IsArabicRequested(Request),
            productIds,
            ct));
    }

    /// WhatsApp destinations the business configured for the storefront, one per purpose. An
    /// empty list is the normal "no WhatsApp configured" answer, and the store shows no action.
    [HttpGet("whatsapp-contacts")]
    [ProducesResponseType(typeof(IReadOnlyList<WhatsAppContactPublicDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWhatsAppContacts(CancellationToken ct)
        => Ok(await _whatsAppContactService.GetPublicAsync(_tenantResolver.GetTenantId(), ct));

    [HttpGet("catalog/products/{productId:guid}")]
    [ProducesResponseType(typeof(OnlineStoreProductPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductPage(Guid productId, CancellationToken ct)
        => Ok(await _catalogService.GetProductPageAsync(
            _tenantResolver.GetTenantId(),
            _branchProvider.GetSelectedBranchId(),
            GeneralHelper.IsArabicRequested(Request),
            productId,
            ct));

    /// Bundle offers that are live for this branch right now. Nothing scheduled for later
    /// and nothing expired is ever returned, so the store cannot advertise a dead deal.
    [HttpGet("offers")]
    [ProducesResponseType(typeof(OnlineStoreOffersDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOffers([FromQuery] Guid? productId, CancellationToken ct)
        => Ok(await _offerService.GetLiveOffersAsync(
            _tenantResolver.GetTenantId(),
            _branchProvider.GetSelectedBranchId(),
            productId,
            ct));

    /// Promotional banners the merchant scheduled for the storefront home.
    [HttpGet("banners")]
    [ProducesResponseType(typeof(IReadOnlyList<StorefrontBannerPublicDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBanners(CancellationToken ct)
        => Ok(await _bannerService.GetLiveAsync(
            _tenantResolver.GetTenantId(),
            StorefrontBannerPlacement.HomeHero,
            ct));

    [HttpGet("catalog/suggest")]
    [ProducesResponseType(typeof(OnlineStoreSuggestionsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuggestions([FromQuery] string? q, CancellationToken ct)
    {
        if (q is { Length: > MaxSearchLength })
            return BadRequest(new { message = $"Search term cannot exceed {MaxSearchLength} characters." });

        return Ok(await _catalogService.GetSuggestionsAsync(
            _tenantResolver.GetTenantId(),
            _branchProvider.GetSelectedBranchId(),
            GeneralHelper.IsArabicRequested(Request),
            q,
            ct));
    }

    /// Guest order lookup for a customer who no longer has the tracking link.
    /// Requires the order number AND a contact detail captured on that order, so an
    /// order number on its own never discloses anything.
    [HttpPost("orders/lookup")]
    [ProducesResponseType(typeof(OnlineStoreOrderLookupResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> LookupOrder(
        OnlineStoreOrderLookupRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        return Ok(await _service.LookupOrderAsync(request.OrderNumber, request.Contact, ct));
    }

    [HttpPost("checkout")]
    [ProducesResponseType(typeof(CreatePaymentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Checkout(
        OnlineShoppingCheckoutRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        return Ok(await _checkoutService.CreateAsync(request, ct));
    }

    [HttpGet("orders/{orderId:guid}/status")]
    [ProducesResponseType(typeof(OnlineShoppingOrderStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrderStatus(
        Guid orderId,
        [FromQuery] string token,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { message = "Order token is required." });
        return Ok(await _service.GetOrderStatusAsync(orderId, token, ct));
    }

    [HttpPost("orders/{orderId:guid}/receipt")]
    [ProducesResponseType(typeof(OnlineShoppingReceiptAccessDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateReceiptAccess(
        Guid orderId,
        [FromQuery] string token,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { message = "Order token is required." });

        await _service.GetOrderStatusAsync(orderId, token, ct);
        var receiptToken = await _receiptService.GetOrCreateAccessTokenAsync(
            orderId,
            _tenantResolver.GetTenantId(),
            ct);
        return Ok(new OnlineShoppingReceiptAccessDto
        {
            ReceiptUrl = $"/receipt/public/{Uri.EscapeDataString(receiptToken)}"
        });
    }

    /// Comma-separated ids from the query string. Anything unparseable is dropped rather than
    /// rejected — a hand-edited or outdated local list must degrade, not break the page.
    private static IReadOnlyCollection<Guid> ParseProductIds(string? ids)
        => (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Guid.TryParse(value, out var id) ? id : (Guid?)null)
            .OfType<Guid>()
            .Distinct()
            .ToList();
}
