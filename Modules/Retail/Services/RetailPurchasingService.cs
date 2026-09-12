using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Retail.DTOs;
using RestaurantPos.Api.Modules.Retail.Domain;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Retail.Services
{
    /// <inheritdoc cref="IRetailPurchasingService"/>
    public sealed class RetailPurchasingService : IRetailPurchasingService
    {
        private const string OrderNumberPrefix = "RPO";
        private const int OrderNumberDigits = 4;
        private const int MaxPageSize = 200;
        private const string PerformedBySystem = "purchase-receipt";

        private readonly PosDbContext _context;
        private readonly IRetailStockLedger _ledger;
        private readonly ITenantResolver _tenantResolver;
        private readonly ILogger<RetailPurchasingService> _logger;

        public RetailPurchasingService(
            PosDbContext context,
            IRetailStockLedger ledger,
            ITenantResolver tenantResolver,
            ILogger<RetailPurchasingService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PaginatedResponse<RetailPurchaseOrderDto>> GetPageAsync(
            Guid branchId,
            RetailPurchaseOrderQuery query,
            bool isArabic,
            CancellationToken ct = default)
        {
            var pageNumber = Math.Max(query.PageNumber, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

            var orders = _context.RetailPurchaseOrders
                .AsNoTracking()
                .Where(o => o.BranchId == branchId);

            if (query.SupplierId.HasValue)
                orders = orders.Where(o => o.SupplierId == query.SupplierId.Value);

            if (query.Status.HasValue)
                orders = orders.Where(o => o.Status == query.Status.Value);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var pattern = $"%{query.Search.Trim()}%";
                orders = orders.Where(o =>
                    EF.Functions.ILike(o.OrderNumber, pattern)
                    || (o.InvoiceNumber != null && EF.Functions.ILike(o.InvoiceNumber, pattern))
                    || EF.Functions.ILike(o.Supplier!.Name, pattern));
            }

            var totalCount = await orders.CountAsync(ct);
            var rows = await orders
                .Include(o => o.Supplier)
                .Include(o => o.Lines)
                .OrderByDescending(o => o.OrderDateUtc)
                .ThenByDescending(o => o.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var brands = await LoadBrandsAsync(rows.SelectMany(o => o.Lines).Select(l => l.ProductId).ToList(), ct);

            return new PaginatedResponse<RetailPurchaseOrderDto>
            {
                Items = rows.Select(o => Map(o, brands, isArabic)).ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<RetailPurchaseOrderDto> GetAsync(Guid id, bool isArabic, CancellationToken ct = default)
        {
            var order = await LoadAsync(id, ct);
            var brands = await LoadBrandsAsync(order.Lines.Select(l => l.ProductId).ToList(), ct);
            return Map(order, brands, isArabic);
        }

        public async Task<RetailPurchaseOrderDto> CreateAsync(
            Guid branchId,
            RetailPurchaseOrderCreateDto input,
            Guid? userId,
            bool isArabic,
            CancellationToken ct = default)
        {
            var lines = await ValidateLinesAsync(input, ct);
            await EnsureSupplierExistsAsync(input.SupplierId, ct);

            var orderNumber = string.IsNullOrWhiteSpace(input.OrderNumber)
                ? await NextOrderNumberAsync(ct)
                : input.OrderNumber.Trim();
            await EnsureOrderNumberIsFreeAsync(orderNumber, null, ct);

            var order = new RetailPurchaseOrder
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantResolver.GetTenantId(),
                BranchId = branchId,
                SupplierId = input.SupplierId,
                OrderNumber = orderNumber,
                InvoiceNumber = Trimmed(input.InvoiceNumber),
                Status = RetailPurchaseOrderStatus.Draft,
                OrderDateUtc = Utc(input.OrderDateUtc ?? DateTime.UtcNow),
                ExpectedDateUtc = input.ExpectedDateUtc.HasValue ? Utc(input.ExpectedDateUtc.Value) : null,
                ShippingCost = input.ShippingCost,
                CustomsCost = input.CustomsCost,
                ClearanceCost = input.ClearanceCost,
                LandedCostAllocation = input.LandedCostAllocation,
                Notes = Trimmed(input.Notes),
                CreatedByUserId = userId
            };

            ApplyLines(order, input, lines);
            _context.RetailPurchaseOrders.Add(order);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Retail purchase order {OrderNumber} created for supplier {SupplierId} with {LineCount} lines totalling {TotalCost}",
                order.OrderNumber, order.SupplierId, order.Lines.Count, order.TotalCost);

            return await GetAsync(order.Id, isArabic, ct);
        }

        public async Task<RetailPurchaseOrderDto> UpdateAsync(
            Guid id,
            RetailPurchaseOrderCreateDto input,
            bool isArabic,
            CancellationToken ct = default)
        {
            var order = await LoadAsync(id, ct);
            if (order.Status != RetailPurchaseOrderStatus.Draft)
                throw new ValidationException("Only a draft purchase order can be edited.");

            var lines = await ValidateLinesAsync(input, ct);
            await EnsureSupplierExistsAsync(input.SupplierId, ct);

            if (!string.IsNullOrWhiteSpace(input.OrderNumber))
            {
                await EnsureOrderNumberIsFreeAsync(input.OrderNumber.Trim(), id, ct);
                order.OrderNumber = input.OrderNumber.Trim();
            }

            order.SupplierId = input.SupplierId;
            order.InvoiceNumber = Trimmed(input.InvoiceNumber);
            order.OrderDateUtc = Utc(input.OrderDateUtc ?? order.OrderDateUtc);
            order.ExpectedDateUtc = input.ExpectedDateUtc.HasValue ? Utc(input.ExpectedDateUtc.Value) : null;
            order.ShippingCost = input.ShippingCost;
            order.CustomsCost = input.CustomsCost;
            order.ClearanceCost = input.ClearanceCost;
            order.LandedCostAllocation = input.LandedCostAllocation;
            order.Notes = Trimmed(input.Notes);

            _context.RetailPurchaseOrderLines.RemoveRange(order.Lines);
            order.Lines.Clear();
            ApplyLines(order, input, lines);

            await _context.SaveChangesAsync(ct);
            return await GetAsync(order.Id, isArabic, ct);
        }

        public async Task<RetailPurchaseOrderDto> SubmitAsync(Guid id, bool isArabic, CancellationToken ct = default)
        {
            var order = await LoadAsync(id, ct);
            if (order.Status != RetailPurchaseOrderStatus.Draft)
                throw new ValidationException("Only a draft purchase order can be submitted.");

            order.Status = RetailPurchaseOrderStatus.Submitted;
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Retail purchase order {OrderNumber} submitted to the supplier", order.OrderNumber);
            return await GetAsync(order.Id, isArabic, ct);
        }

        public async Task<RetailPurchaseOrderDto> ReceiveAsync(
            Guid id,
            RetailPurchaseReceiptDto receipt,
            Guid? userId,
            bool isArabic,
            CancellationToken ct = default)
        {
            var order = await LoadAsync(id, ct);

            // Document-level idempotency. A refreshed page or a resubmitted form finds the stamp
            // and gets the same order back instead of a second set of stock movements.
            if (order.StockPostedAtUtc.HasValue)
            {
                _logger.LogInformation(
                    "Retail purchase order {OrderNumber} was already received at {ReceivedAt}; receipt ignored",
                    order.OrderNumber, order.StockPostedAtUtc);
                return await GetAsync(order.Id, isArabic, ct);
            }

            if (order.Status is RetailPurchaseOrderStatus.Cancelled)
                throw new ValidationException("A cancelled purchase order cannot be received.");

            ApplyReceivedQuantities(order, receipt);
            AllocateLandedCost(order, useReceivedQuantities: true);

            if (!string.IsNullOrWhiteSpace(receipt.InvoiceNumber))
                order.InvoiceNumber = receipt.InvoiceNumber.Trim();

            var receivedAt = DateTime.UtcNow;
            order.Status = RetailPurchaseOrderStatus.Received;
            order.ReceivedAtUtc = receivedAt;
            order.ReceivedByUserId = userId;
            order.StockPostedAtUtc = receivedAt;
            await _context.SaveChangesAsync(ct);

            var postings = order.Lines
                .Where(line => line.QuantityReceived > 0m)
                .Select(line => new RetailStockPosting(
                    ProductId: line.ProductId,
                    BranchId: order.BranchId,
                    MovementType: RetailStockMovementType.PurchaseReceipt,
                    Quantity: line.QuantityReceived,
                    UnitCost: line.LandedUnitCost,
                    SourceDocumentType: RetailStockSourceDocument.RetailPurchaseOrder,
                    SourceDocumentId: order.Id,
                    SourceLineId: line.Id,
                    SourceReference: order.OrderNumber,
                    OccurredAtUtc: receivedAt,
                    PerformedByUserId: userId,
                    PerformedBySystem: PerformedBySystem,
                    Notes: line.SkuSnapshot))
                .ToList();

            await _ledger.PostManyAsync(postings, ct);
            await ApplyWeightedAverageCostAsync(order, ct);

            _logger.LogInformation(
                "Retail purchase order {OrderNumber} received: {Units} units across {Lines} lines, landed total {TotalCost}",
                order.OrderNumber, order.Lines.Sum(l => l.QuantityReceived), postings.Count, order.TotalCost);

            return await GetAsync(order.Id, isArabic, ct);
        }

        public async Task<RetailPurchaseOrderDto> CancelAsync(Guid id, bool isArabic, CancellationToken ct = default)
        {
            var order = await LoadAsync(id, ct);
            if (order.StockPostedAtUtc.HasValue)
                throw new ValidationException("A received purchase order cannot be cancelled because stock was already posted.");

            order.Status = RetailPurchaseOrderStatus.Cancelled;
            await _context.SaveChangesAsync(ct);
            return await GetAsync(order.Id, isArabic, ct);
        }

        public async Task<IReadOnlyList<RetailStockMovementDto>> GetMovementsAsync(
            Guid productId,
            Guid? branchId,
            int limit,
            CancellationToken ct = default)
        {
            var movements = await _ledger.GetMovementsAsync(productId, branchId, limit, ct);
            if (movements.Count == 0)
                return Array.Empty<RetailStockMovementDto>();

            var facts = await _context.RetailProductDetails
                .AsNoTracking()
                .Where(d => d.ProductId == productId)
                .Select(d => new { d.Sku, Name = d.Product!.Name })
                .FirstOrDefaultAsync(ct);

            // The running balance is meaningful only in chronological order, so it is built
            // oldest-first and the list is flipped back for display.
            var ordered = movements.OrderBy(m => m.OccurredAtUtc).ThenBy(m => m.CreatedAt).ToList();
            var balance = 0m;
            var rows = new List<RetailStockMovementDto>(ordered.Count);

            foreach (var movement in ordered)
            {
                balance += movement.Quantity;
                rows.Add(new RetailStockMovementDto
                {
                    Id = movement.Id,
                    ProductId = movement.ProductId,
                    Sku = facts?.Sku ?? string.Empty,
                    ProductName = facts?.Name ?? string.Empty,
                    BranchId = movement.BranchId,
                    MovementType = movement.MovementType.ToString(),
                    Quantity = movement.Quantity,
                    UnitCost = movement.UnitCost,
                    TotalCost = movement.TotalCost,
                    SourceDocumentType = movement.SourceDocumentType.ToString(),
                    SourceDocumentId = movement.SourceDocumentId,
                    SourceReference = movement.SourceReference,
                    OccurredAtUtc = movement.OccurredAtUtc,
                    PerformedBySystem = movement.PerformedBySystem,
                    Notes = movement.Notes,
                    BalanceAfter = balance
                });
            }

            rows.Reverse();
            return rows;
        }

        /// <summary>
        /// Rolls the received landed cost into the product as a weighted average of what was
        /// already on the shelf and what just arrived. This is the costing policy for retail:
        /// there is no expiry-driven rotation to justify FIFO batches, and the workbook already
        /// blended its costs the same way.
        /// </summary>
        private async Task ApplyWeightedAverageCostAsync(RetailPurchaseOrder order, CancellationToken ct)
        {
            var received = order.Lines.Where(l => l.QuantityReceived > 0m).ToList();
            if (received.Count == 0)
                return;

            var productIds = received.Select(l => l.ProductId).Distinct().ToList();
            var onHand = await _ledger.GetOnHandAsync(order.BranchId, productIds, ct);
            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            foreach (var line in received)
            {
                if (!products.TryGetValue(line.ProductId, out var product))
                    continue;

                // The receipt is already posted, so subtract it back out to get the position the
                // arriving goods are being blended into.
                var priorQuantity = Math.Max(0m, onHand.GetValueOrDefault(line.ProductId) - line.QuantityReceived);
                var priorCost = product.CostPrice ?? line.LandedUnitCost;
                var totalQuantity = priorQuantity + line.QuantityReceived;
                if (totalQuantity <= 0m)
                    continue;

                var blended = ((priorQuantity * priorCost) + (line.QuantityReceived * line.LandedUnitCost)) / totalQuantity;
                product.CostPrice = decimal.Round(blended, 2, MidpointRounding.AwayFromZero);
            }

            await _context.SaveChangesAsync(ct);
        }

        private static void ApplyReceivedQuantities(RetailPurchaseOrder order, RetailPurchaseReceiptDto receipt)
        {
            var confirmed = receipt.Lines.ToDictionary(l => l.LineId, l => l.QuantityReceived);

            foreach (var line in order.Lines)
            {
                var quantity = confirmed.TryGetValue(line.Id, out var value) ? value : line.QuantityOrdered;

                if (quantity < 0m)
                    throw new ValidationException($"Received quantity for {line.SkuSnapshot} cannot be negative.");

                // Over-receipt is rejected rather than absorbed: goods beyond the order are a
                // different commercial event and need their own document.
                if (quantity > line.QuantityOrdered)
                    throw new ValidationException(
                        $"Received quantity for {line.SkuSnapshot} ({quantity:0.##}) exceeds the ordered quantity ({line.QuantityOrdered:0.##}).");

                line.QuantityReceived = quantity;
            }

            if (order.Lines.All(line => line.QuantityReceived <= 0m))
                throw new ValidationException("A receipt must confirm at least one unit.");
        }

        private void ApplyLines(
            RetailPurchaseOrder order,
            RetailPurchaseOrderCreateDto input,
            IReadOnlyDictionary<Guid, RetailLineFacts> facts)
        {
            var sortOrder = 0;
            foreach (var line in input.Lines)
            {
                var fact = facts[line.ProductId];
                order.Lines.Add(new RetailPurchaseOrderLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = order.TenantId,
                    RetailPurchaseOrderId = order.Id,
                    ProductId = line.ProductId,
                    SkuSnapshot = fact.Sku,
                    ProductNameSnapshot = fact.Name,
                    QuantityOrdered = line.Quantity,
                    QuantityReceived = 0m,
                    UnitCost = line.UnitCost,
                    SortOrder = sortOrder++
                });
            }

            AllocateLandedCost(order, useReceivedQuantities: false);
        }

        /// <summary>
        /// Spreads shipping, customs and clearance over the lines and derives each line's landed
        /// unit cost. Rounding remainders land on the last line so the allocated charges always
        /// add back up to the charges entered.
        /// </summary>
        private static void AllocateLandedCost(RetailPurchaseOrder order, bool useReceivedQuantities)
        {
            var lines = order.Lines.OrderBy(l => l.SortOrder).ToList();
            if (lines.Count == 0)
            {
                order.GoodsCost = 0m;
                order.TotalCost = 0m;
                return;
            }

            decimal Basis(RetailPurchaseOrderLine line)
                => useReceivedQuantities ? line.QuantityReceived : line.QuantityOrdered;

            var goodsCost = lines.Sum(l => Round2(Basis(l) * l.UnitCost));
            var charges = order.LandedCharges;
            var totalQuantity = lines.Sum(Basis);
            var allocated = 0m;

            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var quantity = Basis(line);
                var lineGoods = Round2(quantity * line.UnitCost);

                decimal share;
                if (charges <= 0m || quantity <= 0m)
                {
                    share = 0m;
                }
                else if (index == lines.Count - 1)
                {
                    share = Round2(charges - allocated);
                }
                else
                {
                    share = order.LandedCostAllocation == RetailLandedCostAllocation.ByValue
                        ? Round2(goodsCost <= 0m ? 0m : charges * lineGoods / goodsCost)
                        : Round2(totalQuantity <= 0m ? 0m : charges * quantity / totalQuantity);
                }

                allocated += share;
                line.AllocatedCharges = share;
                line.LandedUnitCost = quantity <= 0m
                    ? line.UnitCost
                    : decimal.Round(line.UnitCost + (share / quantity), 4, MidpointRounding.AwayFromZero);
            }

            order.GoodsCost = Round2(goodsCost);
            order.TotalCost = Round2(goodsCost + charges);
        }

        private async Task<Dictionary<Guid, RetailLineFacts>> ValidateLinesAsync(
            RetailPurchaseOrderCreateDto input,
            CancellationToken ct)
        {
            if (input.Lines.Count == 0)
                throw new ValidationException("A purchase order needs at least one line.");

            var duplicate = input.Lines
                .GroupBy(l => l.ProductId)
                .FirstOrDefault(g => g.Count() > 1);
            if (duplicate is not null)
                throw new ValidationException("The same product appears on more than one line. Merge them into one line.");

            var productIds = input.Lines.Select(l => l.ProductId).ToList();
            var facts = await _context.RetailProductDetails
                .AsNoTracking()
                .Where(d => productIds.Contains(d.ProductId))
                .Select(d => new RetailLineFacts(d.ProductId, d.Sku, d.Product!.Name))
                .ToDictionaryAsync(f => f.ProductId, ct);

            var missing = productIds.Where(id => !facts.ContainsKey(id)).ToList();
            if (missing.Count > 0)
                throw new ValidationException("Every line must be a retail product with a SKU. Add the retail details first.");

            foreach (var line in input.Lines)
            {
                if (line.Quantity <= 0m)
                    throw new ValidationException($"Quantity for {facts[line.ProductId].Sku} must be greater than zero.");
                if (line.UnitCost < 0m)
                    throw new ValidationException($"Unit cost for {facts[line.ProductId].Sku} cannot be negative.");
            }

            return facts;
        }

        private async Task EnsureSupplierExistsAsync(Guid supplierId, CancellationToken ct)
        {
            var exists = await _context.Suppliers.AsNoTracking().AnyAsync(s => s.Id == supplierId, ct);
            if (!exists)
                throw new ValidationException("Supplier not found.");
        }

        private async Task EnsureOrderNumberIsFreeAsync(string orderNumber, Guid? excludeId, CancellationToken ct)
        {
            var taken = await _context.RetailPurchaseOrders
                .AsNoTracking()
                .AnyAsync(o => o.OrderNumber == orderNumber && (excludeId == null || o.Id != excludeId), ct);

            if (taken)
                throw new ConflictException($"Purchase order number '{orderNumber}' already exists.");
        }

        private async Task<string> NextOrderNumberAsync(CancellationToken ct)
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"{OrderNumberPrefix}-{year}-";
            var used = await _context.RetailPurchaseOrders
                .AsNoTracking()
                .Where(o => o.OrderNumber.StartsWith(prefix))
                .Select(o => o.OrderNumber)
                .ToListAsync(ct);

            var next = used
                .Select(number => int.TryParse(number[prefix.Length..], out var value) ? value : 0)
                .DefaultIfEmpty(0)
                .Max() + 1;

            return $"{prefix}{next.ToString().PadLeft(OrderNumberDigits, '0')}";
        }

        private async Task<RetailPurchaseOrder> LoadAsync(Guid id, CancellationToken ct)
            => await _context.RetailPurchaseOrders
                   .Include(o => o.Supplier)
                   .Include(o => o.Lines)
                   .FirstOrDefaultAsync(o => o.Id == id, ct)
               ?? throw new NotFoundException(nameof(RetailPurchaseOrder), id);

        /// <summary>Brand per product, for display only — the line already snapshots SKU and name.</summary>
        private async Task<Dictionary<Guid, string?>> LoadBrandsAsync(
            IReadOnlyCollection<Guid> productIds,
            CancellationToken ct)
        {
            if (productIds.Count == 0)
                return new Dictionary<Guid, string?>();

            return await _context.RetailProductDetails
                .AsNoTracking()
                .Where(d => productIds.Contains(d.ProductId))
                .Select(d => new { d.ProductId, d.Brand })
                .ToDictionaryAsync(x => x.ProductId, x => x.Brand, ct);
        }

        private static RetailPurchaseOrderDto Map(
            RetailPurchaseOrder order,
            IReadOnlyDictionary<Guid, string?> brands,
            bool isArabic)
            => new()
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                InvoiceNumber = order.InvoiceNumber,
                Status = order.Status.ToString(),
                BranchId = order.BranchId,
                SupplierId = order.SupplierId,
                SupplierName = isArabic && !string.IsNullOrWhiteSpace(order.Supplier?.NameAr)
                    ? order.Supplier!.NameAr!
                    : order.Supplier?.Name ?? string.Empty,
                SupplierNameAr = order.Supplier?.NameAr,
                OrderDateUtc = order.OrderDateUtc,
                ExpectedDateUtc = order.ExpectedDateUtc,
                ReceivedAtUtc = order.ReceivedAtUtc,
                ShippingCost = order.ShippingCost,
                CustomsCost = order.CustomsCost,
                ClearanceCost = order.ClearanceCost,
                LandedCostAllocation = order.LandedCostAllocation.ToString(),
                GoodsCost = order.GoodsCost,
                TotalCost = order.TotalCost,
                TotalUnitsOrdered = order.Lines.Sum(l => l.QuantityOrdered),
                TotalUnitsReceived = order.Lines.Sum(l => l.QuantityReceived),
                Notes = order.Notes,
                StockPosted = order.StockPostedAtUtc.HasValue,
                Lines = order.Lines
                    .OrderBy(l => l.SortOrder)
                    .Select(l => new RetailPurchaseOrderLineDto
                    {
                        Id = l.Id,
                        ProductId = l.ProductId,
                        Sku = l.SkuSnapshot,
                        ProductName = l.ProductNameSnapshot,
                        Brand = brands.GetValueOrDefault(l.ProductId),
                        QuantityOrdered = l.QuantityOrdered,
                        QuantityReceived = l.QuantityReceived,
                        UnitCost = l.UnitCost,
                        LineCost = Round2(l.QuantityOrdered * l.UnitCost),
                        AllocatedCharges = l.AllocatedCharges,
                        LandedUnitCost = l.LandedUnitCost
                    })
                    .ToList()
            };

        private static decimal Round2(decimal value)
            => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

        private static DateTime Utc(DateTime value)
            => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Utc);

        private static string? Trimmed(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private sealed record RetailLineFacts(Guid ProductId, string Sku, string Name);
    }
}
