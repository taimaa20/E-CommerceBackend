using MediatR;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Repositories;
using RestaurantPos.Api.Services.Caching;
using RestaurantPos.Api.Services.Pricing;

namespace RestaurantPos.Api.Handlers
{
    public sealed class RawMaterialCostChangedHandler
        : INotificationHandler<RawMaterialCostChangedEvent>
    {
        private readonly IProductRepository _productRepository;
        private readonly IPricingEngine _pricingEngine;
        private readonly ICacheService _cache;
        private readonly ILogger<RawMaterialCostChangedHandler> _logger;

        public RawMaterialCostChangedHandler(
            IProductRepository productRepository,
            IPricingEngine pricingEngine,
            ICacheService cache,
            ILogger<RawMaterialCostChangedHandler> logger)
        {
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _pricingEngine = pricingEngine ?? throw new ArgumentNullException(nameof(pricingEngine));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Handle(
            RawMaterialCostChangedEvent notification,
            CancellationToken cancellationToken)
        {
            var products = await _productRepository.GetProductsByRawMaterialIdWithRecipeItemsAsync(
                notification.RawMaterialId,
                cancellationToken);
            if (products.Count == 0)
                return;

            foreach (var product in products)
            {
                var costPrice = product.RecipeItems.Sum(
                    row => row.Amount * row.RawMaterial.CostPerUnit);
                _pricingEngine.ApplyCostChange(product, costPrice);
            }

            await _productRepository.UpdateProductsAsync(products, cancellationToken);
            await _cache.RemoveByPatternAsync(
                CacheKeys.ProductsPattern(notification.TenantId),
                cancellationToken);
            await _cache.RemoveByPatternAsync(
                $"pos:menu:*:{notification.TenantId}:*",
                cancellationToken);

            _logger.LogInformation(
                "Recalculated pricing metrics for {ProductCount} products after raw material {RawMaterialId} cost changed",
                products.Count,
                notification.RawMaterialId);
        }
    }
}
