using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Services.Categroy;

namespace RestaurantPos.Api.Services;

public sealed class OnlineStoreCatalogService : IOnlineStoreCatalogService
{
    /// How many products each merchandising row on the home page carries. Small on purpose:
    /// the home page is for discovery, the catalogue page is for the full 100+ item wall.
    private const int HomeSectionSize = 12;
    private const int RelatedProductLimit = 8;
    private const int SuggestionProductLimit = 6;
    private const int SuggestionCategoryLimit = 4;

    private readonly IOnlineShoppingService _onlineShoppingService;
    private readonly IProductService _productService;
    private readonly ICategoryServices _categoryService;
    private readonly IStorefrontCatalogRepository _storefrontRepository;

    public OnlineStoreCatalogService(
        IOnlineShoppingService onlineShoppingService,
        IProductService productService,
        ICategoryServices categoryService,
        IStorefrontCatalogRepository storefrontRepository)
    {
        _onlineShoppingService = onlineShoppingService ?? throw new ArgumentNullException(nameof(onlineShoppingService));
        _productService = productService ?? throw new ArgumentNullException(nameof(productService));
        _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
        _storefrontRepository = storefrontRepository ?? throw new ArgumentNullException(nameof(storefrontRepository));
    }

    public async Task<OnlineStoreCatalogDto> GetCatalogAsync(
        Guid tenantId,
        Guid? branchId,
        bool isArabic,
        OnlineStoreCatalogQuery query,
        CancellationToken ct)
    {
        var store = await LoadStoreAsync(tenantId, branchId, isArabic, ct);

        var categories = BuildCategories(store);
        // Sellable products always lead, whichever ordering the shopper picked, so
        // an out-of-stock row never occupies the top of a page.
        var matches = Sort(
            store.Products
                .Where(product => MatchesCategory(product, query.CategoryId))
                .Where(product => MatchesSearch(product, query.Search))
                .OrderByDescending(store.IsSellable),
            query.Sort)
            .ToList();

        var totalPages = Math.Max(1, (int)Math.Ceiling(matches.Count / (double)query.PageSize));
        var page = Math.Min(Math.Max(query.Page, 1), totalPages);
        var pageItems = matches.Skip((page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return new OnlineStoreCatalogDto
        {
            Categories = categories,
            Products = pageItems,
            ProductAvailability = store.AvailabilityFor(pageItems),
            ProductDetails = await BuildDetailsAsync(tenantId, pageItems, ct),
            CategoryId = query.CategoryId,
            Page = page,
            PageSize = query.PageSize,
            TotalCount = matches.Count,
            TotalPages = totalPages
        };
    }

    public async Task<OnlineStoreHomeDto> GetHomeAsync(
        Guid tenantId,
        Guid? branchId,
        bool isArabic,
        CancellationToken ct)
    {
        var store = await LoadStoreAsync(tenantId, branchId, isArabic, ct);
        var sellable = store.Products.Where(store.IsSellable).ToList();

        var sections = new List<OnlineStoreSectionDto>();

        var onSale = sellable
            .Where(HasRealDiscount)
            .OrderByDescending(DiscountShare)
            .ThenBy(Label, StringComparer.CurrentCultureIgnoreCase)
            .Take(HomeSectionSize)
            .ToList();
        if (onSale.Count > 0)
            sections.Add(new OnlineStoreSectionDto { Key = "onSale", Products = onSale });

        var newArrivals = await BuildNewArrivalsAsync(tenantId, sellable, ct);
        if (newArrivals.Count > 0)
            sections.Add(new OnlineStoreSectionDto { Key = "newArrivals", Products = newArrivals });

        var explore = BuildExplore(sellable, alreadyShown: sections.SelectMany(row => row.Products).Select(p => p.Id).ToHashSet());
        if (explore.Count > 0)
            sections.Add(new OnlineStoreSectionDto { Key = "explore", Products = explore });

        var shown = sections.SelectMany(section => section.Products).DistinctBy(product => product.Id).ToList();

        return new OnlineStoreHomeDto
        {
            Categories = BuildCategories(store),
            Sections = sections,
            ProductAvailability = store.AvailabilityFor(shown),
            ProductDetails = await BuildDetailsAsync(tenantId, shown, ct),
            TotalProductCount = store.Products.Count,
        };
    }

    public async Task<OnlineStoreProductPageDto> GetProductPageAsync(
        Guid tenantId,
        Guid? branchId,
        bool isArabic,
        Guid productId,
        CancellationToken ct)
    {
        var store = await LoadStoreAsync(tenantId, branchId, isArabic, ct);
        var product = store.Products.FirstOrDefault(row => row.Id == productId)
            ?? throw new NotFoundException("Product", productId);

        // Same category, current product excluded, sellable first, then alphabetical.
        // A plain catalogue rule — nothing here is personalised or ranked by behaviour.
        var related = product.CategoryId.HasValue
            ? store.Products
                .Where(row => row.Id != productId && row.CategoryId == product.CategoryId)
                .OrderByDescending(store.IsSellable)
                .ThenBy(Label, StringComparer.CurrentCultureIgnoreCase)
                .Take(RelatedProductLimit)
                .ToList()
            : [];

        // One media/attribute read for the product and its neighbours together.
        var details = await BuildDetailsAsync(tenantId, [product, .. related], ct);

        return new OnlineStoreProductPageDto
        {
            Product = product,
            Availability = store.Availability[productId],
            Details = details.FirstOrDefault(row => row.ProductId == productId)
                ?? new OnlineStoreProductDetailsDto { ProductId = productId },
            Category = BuildCategories(store).FirstOrDefault(row => row.Id == product.CategoryId),
            Related = related,
            RelatedAvailability = store.AvailabilityFor(related),
            RelatedDetails = details.Where(row => row.ProductId != productId).ToList(),
        };
    }

    public async Task<OnlineStoreProductSetDto> GetProductSetAsync(
        Guid tenantId,
        Guid? branchId,
        bool isArabic,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken ct)
    {
        if (productIds.Count == 0) return new OnlineStoreProductSetDto();

        var store = await LoadStoreAsync(tenantId, branchId, isArabic, ct);
        var wanted = productIds.ToHashSet();
        // Ordered by the caller's own list, so a favourites page keeps the order the shopper
        // built. Unknown ids fall out here — that is the "product was removed" case, and it
        // costs the caller a shorter list rather than a failed page.
        var byId = store.Products.Where(product => wanted.Contains(product.Id)).ToDictionary(product => product.Id);
        var products = productIds
            .Distinct()
            .Select(id => byId.GetValueOrDefault(id))
            .OfType<ProductDto>()
            .ToList();

        return new OnlineStoreProductSetDto
        {
            Products = products,
            ProductAvailability = store.AvailabilityFor(products),
            ProductDetails = await BuildDetailsAsync(tenantId, products, ct),
        };
    }

    public async Task<OnlineStoreSuggestionsDto> GetSuggestionsAsync(
        Guid tenantId,
        Guid? branchId,
        bool isArabic,
        string? search,
        CancellationToken ct)
    {
        var term = search?.Trim();
        if (string.IsNullOrEmpty(term)) return new OnlineStoreSuggestionsDto();

        var store = await LoadStoreAsync(tenantId, branchId, isArabic, ct);
        var matches = store.Products
            .Where(product => MatchesSearch(product, term))
            .OrderByDescending(store.IsSellable)
            // A hit in the name is a better suggestion than a hit buried in a description.
            .ThenByDescending(product => Contains(product.Name, term) || Contains(product.NameAr, term))
            .ThenBy(Label, StringComparer.CurrentCultureIgnoreCase)
            .Take(SuggestionProductLimit)
            .ToList();

        var attributes = (await _storefrontRepository.GetProductAttributesAsync(
                tenantId,
                matches.Select(product => product.Id).ToList(),
                ct))
            .ToDictionary(row => row.ProductId);

        var categories = BuildCategories(store)
            .Where(category => Contains(category.Name, term) || Contains(category.NameAr, term))
            .Take(SuggestionCategoryLimit)
            .ToList();

        return new OnlineStoreSuggestionsDto
        {
            Products = matches.Select(product => new OnlineStoreSuggestionDto
            {
                Id = product.Id,
                Name = product.Name,
                NameAr = product.NameAr,
                Price = DisplayPrice(product),
                BasePrice = product.BasePrice,
                ImageUrl = product.ImageUrl,
                ImageKey = product.ImageKey,
                Brand = attributes.TryGetValue(product.Id, out var row) ? row.Brand : null,
            }).ToList(),
            Categories = categories,
        };
    }

    // ── Composition ──────────────────────────────────────────────────────────

    /// The branch catalogue is the source of truth for "sellable here"; the public product
    /// list supplies the customer-facing fields and the authoritative price.
    private async Task<StoreSnapshot> LoadStoreAsync(
        Guid tenantId,
        Guid? branchId,
        bool isArabic,
        CancellationToken ct)
    {
        var availability = await _onlineShoppingService.GetProductAvailabilityAsync(tenantId, branchId, ct);
        var availabilityById = availability.ToDictionary(row => row.ProductId);
        var catalogue = await _productService.GetPublicActiveProductsAsync(tenantId, isArabic, ct);
        var categories = await _categoryService.GetPublicCategories(isArabic);

        return new StoreSnapshot
        {
            Products = catalogue.Where(product => availabilityById.ContainsKey(product.Id)).ToList(),
            Availability = availabilityById,
            Categories = categories,
        };
    }

    private static IReadOnlyList<OnlineStoreCategoryDto> BuildCategories(StoreSnapshot store)
    {
        var byCategory = store.Products
            .Where(product => product.CategoryId.HasValue)
            .GroupBy(product => product.CategoryId!.Value)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ProductDto>)group.ToList());

        return store.Categories
            .Where(category => byCategory.ContainsKey(category.Id))
            .Select(category => BuildCategory(category, byCategory[category.Id], store))
            .ToList();
    }

    /// <summary>
    /// One department as the store may present it.
    ///
    /// Its discount marks count only products a shopper can actually buy in this branch and
    /// that are genuinely reduced — an unsellable reduced product would advertise a saving
    /// the store cannot honour. A department with nothing reduced reports zero and the
    /// storefront then shows no mark at all, which is what keeps the marks meaningful.
    /// </summary>
    private static OnlineStoreCategoryDto BuildCategory(
        CategoryDto category,
        IReadOnlyList<ProductDto> products,
        StoreSnapshot store)
    {
        var reduced = products
            .Where(product => store.IsSellable(product) && HasRealDiscount(product))
            .ToList();

        return new OnlineStoreCategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            NameAr = category.NameAr,
            Description = category.Description,
            ImageUrl = category.ImageUrl,
            SortOrder = category.SortOrder,
            ProductCount = products.Count,
            DiscountedProductCount = reduced.Count,
            MaxDiscountPercent = reduced.Count == 0 ? 0 : reduced.Max(DiscountPercent)
        };
    }

    private async Task<IReadOnlyList<OnlineStoreProductDetailsDto>> BuildDetailsAsync(
        Guid tenantId,
        IReadOnlyList<ProductDto> products,
        CancellationToken ct)
    {
        if (products.Count == 0) return [];

        var ids = products.Select(product => product.Id).ToList();
        var images = (await _storefrontRepository.GetProductImagesAsync(tenantId, ids, ct))
            .GroupBy(row => row.ProductId)
            .ToDictionary(group => group.Key, group => group.OrderBy(row => row.SortOrder).ToList());
        var attributes = (await _storefrontRepository.GetProductAttributesAsync(tenantId, ids, ct))
            .ToDictionary(row => row.ProductId);

        return products.Select(product =>
        {
            attributes.TryGetValue(product.Id, out var attribute);
            return new OnlineStoreProductDetailsDto
            {
                ProductId = product.Id,
                Brand = Clean(attribute?.Brand),
                BrandName = Clean(attribute?.BrandName),
                BrandNameAr = Clean(attribute?.BrandNameAr),
                BrandLogoUrl = Clean(attribute?.BrandLogoUrl),
                BrandLogoKey = Clean(attribute?.BrandLogoKey),
                Sku = Clean(attribute?.Sku),
                // The workbook writes the literal "N/A" when a product has no size.
                SizeLabel = CleanSize(attribute?.SizeLabel),
                CountryOfOrigin = Clean(attribute?.CountryOfOrigin),
                Images = images.TryGetValue(product.Id, out var rows)
                    ? rows.Select(row => new OnlineStoreImageDto
                    {
                        ImageUrl = row.ImageUrl,
                        ImageKey = row.ImageKey,
                        AltText = row.AltText,
                    }).ToList()
                    : [],
            };
        }).ToList();
    }

    /// Newest first by creation date. Legitimate because the catalogue is genuinely built up
    /// over time; when every product shares one creation date the row degenerates to an
    /// alphabetical slice, which is why it is labelled by date, never as "trending".
    private async Task<IReadOnlyList<ProductDto>> BuildNewArrivalsAsync(
        Guid tenantId,
        IReadOnlyList<ProductDto> sellable,
        CancellationToken ct)
    {
        var createdAt = await _storefrontRepository.GetProductCreatedAtAsync(tenantId, ct);
        if (createdAt.Count == 0) return [];

        // A single creation date across the whole catalogue carries no "new" signal.
        var distinctDays = createdAt.Values.Select(value => value.Date).Distinct().Take(2).Count();
        if (distinctDays < 2) return [];

        return sellable
            .Where(product => createdAt.ContainsKey(product.Id))
            .OrderByDescending(product => createdAt[product.Id])
            .ThenBy(Label, StringComparer.CurrentCultureIgnoreCase)
            .Take(HomeSectionSize)
            .ToList();
    }

    /// One product from each of the busiest departments, so the row reads as a tour of the
    /// store rather than another slice of the same category.
    private static IReadOnlyList<ProductDto> BuildExplore(
        IReadOnlyList<ProductDto> sellable,
        IReadOnlySet<Guid> alreadyShown)
        => sellable
            .Where(product => product.CategoryId.HasValue && !alreadyShown.Contains(product.Id))
            .GroupBy(product => product.CategoryId!.Value)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.OrderBy(Label, StringComparer.CurrentCultureIgnoreCase).First())
            .Take(HomeSectionSize)
            .ToList();

    // ── Rules ────────────────────────────────────────────────────────────────

    private static IOrderedEnumerable<ProductDto> Sort(
        IOrderedEnumerable<ProductDto> products,
        OnlineStoreCatalogSort sort)
        => sort switch
        {
            OnlineStoreCatalogSort.PriceAscending => products
                .ThenBy(DisplayPrice)
                .ThenBy(Label, StringComparer.CurrentCultureIgnoreCase),
            OnlineStoreCatalogSort.PriceDescending => products
                .ThenByDescending(DisplayPrice)
                .ThenBy(Label, StringComparer.CurrentCultureIgnoreCase),
            _ => products.ThenBy(Label, StringComparer.CurrentCultureIgnoreCase)
        };

    /// Mirrors the price the storefront card renders so the ordering the shopper
    /// sees matches the numbers on the tiles.
    private static decimal DisplayPrice(ProductDto product)
        => product.DiscountedPrice ?? product.BasePrice;

    private static bool HasRealDiscount(ProductDto product)
        => product.DiscountedPrice.HasValue
            && product.BasePrice > 0
            && product.DiscountedPrice.Value < product.BasePrice;

    /// <summary>Whole percent, floored at 1 so a real but tiny reduction never reads as 0%.</summary>
    private static int DiscountPercent(ProductDto product)
        => Math.Max(1, (int)Math.Round(DiscountShare(product) * 100m, MidpointRounding.AwayFromZero));

    private static decimal DiscountShare(ProductDto product)
        => HasRealDiscount(product)
            ? (product.BasePrice - product.DiscountedPrice!.Value) / product.BasePrice
            : 0m;

    private static bool MatchesCategory(ProductDto product, Guid? categoryId)
        => !categoryId.HasValue || product.CategoryId == categoryId.Value;

    private static bool MatchesSearch(ProductDto product, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        var term = search.Trim();
        return Contains(product.Name, term)
            || Contains(product.NameAr, term)
            || Contains(product.Description, term)
            || Contains(product.DescriptionAr, term)
            || Contains(product.CategoryName, term)
            || Contains(product.CategoryNameAr, term);
    }

    private static bool Contains(string? value, string term)
        => !string.IsNullOrEmpty(value) && value.Contains(term, StringComparison.CurrentCultureIgnoreCase);

    private static string Label(ProductDto product)
        => string.IsNullOrWhiteSpace(product.DisplayName) ? product.Name : product.DisplayName;

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? CleanSize(string? value)
    {
        var cleaned = Clean(value);
        return cleaned is null || cleaned.Equals("N/A", StringComparison.OrdinalIgnoreCase) ? null : cleaned;
    }

    /// One consistent read of the store for a single request: what exists, what is sellable
    /// here and which departments those products belong to.
    private sealed class StoreSnapshot
    {
        public required IReadOnlyList<ProductDto> Products { get; init; }
        public required IReadOnlyDictionary<Guid, OnlineShoppingProductAvailabilityDto> Availability { get; init; }
        public required IReadOnlyList<CategoryDto> Categories { get; init; }

        public bool IsSellable(ProductDto product)
            => product.IsAvailableNow && Availability[product.Id].IsAvailableOnline;

        public IReadOnlyList<OnlineShoppingProductAvailabilityDto> AvailabilityFor(IEnumerable<ProductDto> products)
            => products.Select(product => Availability[product.Id]).ToList();
    }
}
