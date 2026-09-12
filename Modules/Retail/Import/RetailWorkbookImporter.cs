using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Retail.Domain;
using RestaurantPos.Api.Modules.Retail.Services;

namespace RestaurantPos.Api.Modules.Retail.Import
{
    public interface IRetailWorkbookImporter
    {
        /// <summary>Synchronises the workbook into the retail branch. With
        /// <paramref name="dryRun"/> the plan is computed and reported and nothing is written.</summary>
        Task<RetailWorkbookImportResult> RunAsync(string workbookPath, bool dryRun, CancellationToken ct = default);
    }

    /// <summary>
    /// Incremental, idempotent sync of the DOHA LUXE workbook into the retail branch.
    ///
    /// The workbook is not the database. It owns its own domain — the product master, its
    /// received quantities, the sales it recorded and the purchase headers — and nothing else.
    /// Anything the shop did through the POS afterwards (orders, receipts, adjustments) is
    /// operational fact and is never rewritten to make a spreadsheet total agree.
    ///
    /// Every decision is made by <see cref="RetailWorkbookPlanner"/> against a read-only
    /// snapshot before a row is touched, so the dry run and the real run cannot disagree.
    /// Identity is natural throughout: normalised SKU for a product, supplier name for a
    /// supplier, the transaction itself for a historical sale, PO number for a purchase.
    ///
    /// Writes entities directly rather than through <c>ProductService</c> on purpose: that
    /// service auto-assigns every new product to the Main Branch, which would put retail
    /// stock in the restaurant POS.
    /// </summary>
    public sealed class RetailWorkbookImporter : IRetailWorkbookImporter
    {
        public static readonly Guid RetailBranchId = Guid.Parse("2f4c7d10-9b31-4e6a-8f52-6a1c0d7b4e01");
        private const string RetailBranchCode = "RETAIL";
        private const string RetailBranchName = "DOHA LUXE Retail";
        private const string RetailBranchNameAr = "دوحة لوكس للتجزئة";
        private const string OrderNumberPrefix = "DL-";
        private const string PerformedBySystem = "workbook-import";

        private static readonly UserRole[] RetailUserRoles =
        {
            UserRole.Admin, UserRole.SuperAdmin, UserRole.Manager, UserRole.Cashier
        };

        private readonly PosDbContext _context;
        private readonly IRetailStockLedger _ledger;
        private readonly IRetailOrderStockService _orderStock;
        private readonly ILogger<RetailWorkbookImporter> _logger;

        public RetailWorkbookImporter(
            PosDbContext context,
            IRetailStockLedger ledger,
            IRetailOrderStockService orderStock,
            ILogger<RetailWorkbookImporter> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _orderStock = orderStock ?? throw new ArgumentNullException(nameof(orderStock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<RetailWorkbookImportResult> RunAsync(string workbookPath, bool dryRun, CancellationToken ct = default)
        {
            if (!File.Exists(workbookPath))
                throw new FileNotFoundException($"Workbook not found: {workbookPath}", workbookPath);

            var tenantId = await ResolveTenantIdAsync(ct);
            var report = new RetailWorkbookImportResult
            {
                WorkbookPath = Path.GetFullPath(workbookPath),
                TenantId = tenantId,
                RetailBranchId = RetailBranchId,
                RetailBranchCode = RetailBranchCode,
                RetailBranchName = RetailBranchName,
                IsDryRun = dryRun
            };

            using var reader = new RetailWorkbookReader(workbookPath);
            var snapshot = await RetailWorkbookSnapshot.LoadAsync(_context, tenantId, RetailBranchId, OrderNumberPrefix, ct);
            var plan = new RetailWorkbookPlanner(reader, snapshot, report).Build();

            if (dryRun)
            {
                _logger.LogInformation("Retail workbook dry run finished for {Workbook}; nothing was written.", report.WorkbookPath);
                return report;
            }

            await ApplyAsync(plan, tenantId, ct);
            await MeasureAsync(report, ct);

            _logger.LogInformation(
                "Retail workbook sync applied. Products {Products}, sales {Sales}, purchases {Purchases}, stock movements {Movements}",
                report.ProductTally, report.SaleTally, report.PurchaseTally, report.AppliedMovements);

            return report;
        }

        private async Task ApplyAsync(RetailSyncPlan plan, Guid tenantId, CancellationToken ct)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);

            await EnsureRetailBranchAsync(tenantId, ct);
            await EnsureUserBranchAccessAsync(tenantId, plan.Report, ct);
            await EnsureRetailPaymentMethodsAsync(tenantId, plan.Report, ct);

            var suppliers = await ApplySuppliersAsync(plan, tenantId, ct);
            var categories = await ApplyCategoriesAsync(plan, tenantId, ct);
            var products = await ApplyProductsAsync(plan, tenantId, suppliers, categories, ct);

            await ApplyBranchAssignmentsAsync(plan, tenantId, products, categories, ct);
            await ApplySalesAsync(plan, tenantId, products, ct);
            await ApplyPaymentNormalisationAsync(plan, tenantId, ct);
            await ApplyPurchasesAsync(plan, tenantId, suppliers, ct);
            await ApplyStockAsync(plan, products, ct);

            await transaction.CommitAsync(ct);
        }

        // ── Branch, access, payment methods ──────────────────────────────────

        private async Task<Guid> ResolveTenantIdAsync(CancellationToken ct)
        {
            var tenantId = await _context.Tenants
                .AsNoTracking()
                .Where(t => t.Id == SeedData.DefaultTenantId)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync(ct);

            return tenantId ?? SeedData.DefaultTenantId;
        }

        private async Task EnsureRetailBranchAsync(Guid tenantId, CancellationToken ct)
        {
            if (await _context.Branches.AnyAsync(b => b.Id == RetailBranchId, ct))
                return;

            _context.Branches.Add(new Branch
            {
                Id = RetailBranchId,
                TenantId = tenantId,
                Name = RetailBranchName,
                NameAr = RetailBranchNameAr,
                Code = RetailBranchCode,
                Description = "Retail branch imported from the DOHA LUXE workbook.",
                IsMainBranch = false,
                IsActive = true
            });

            await _context.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Grants retail-capable roles access to the retail branch. Also guarantees every
        /// affected user keeps exactly one default assignment — <c>BranchResolver</c> rejects
        /// a user who has several branches and no default, so adding a second assignment
        /// without this would lock existing users out.
        /// </summary>
        private async Task EnsureUserBranchAccessAsync(Guid tenantId, RetailWorkbookImportResult report, CancellationToken ct)
        {
            var userIds = await _context.Users
                .Where(u => u.TenantId == tenantId && RetailUserRoles.Contains(u.Role))
                .Select(u => u.Id)
                .ToListAsync(ct);

            var assignments = await _context.UserBranches
                .Where(ub => userIds.Contains(ub.UserId))
                .ToListAsync(ct);

            foreach (var userId in userIds)
            {
                var existing = assignments.Where(a => a.UserId == userId).ToList();

                if (existing.Count > 0 && existing.All(a => !a.IsDefault))
                {
                    var fallback = existing.FirstOrDefault(a => a.BranchId == BranchDefaults.MainBranchId) ?? existing[0];
                    fallback.IsDefault = true;
                }

                if (existing.Any(a => a.BranchId == RetailBranchId))
                {
                    report.UserBranchAssignments.Unchanged++;
                    continue;
                }

                _context.UserBranches.Add(new UserBranch
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    UserId = userId,
                    BranchId = RetailBranchId,
                    IsDefault = existing.Count == 0
                });
                report.UserBranchAssignments.Created++;
            }

            await _context.SaveChangesAsync(ct);
        }

        /// <summary>
        /// The workbook settles in cash and bank transfer, so both methods must exist and be
        /// enabled on the retail branch or its till has nothing to check out with. Methods are
        /// enabled ONLY on the retail branch — the Main Branch's enabled set is never touched.
        /// </summary>
        private async Task EnsureRetailPaymentMethodsAsync(Guid tenantId, RetailWorkbookImportResult report, CancellationToken ct)
        {
            var existing = await _context.PaymentMethods.ToListAsync(ct);
            var byCode = existing.ToDictionary(m => m.Code, m => m, StringComparer.OrdinalIgnoreCase);

            foreach (var seed in SeedData.DefaultPaymentMethods.Where(m => RetailPaymentMapping.RequiredCodes.Contains(m.Code)))
            {
                if (byCode.ContainsKey(seed.Code))
                    continue;

                var method = new PaymentMethod
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    NameEn = seed.NameEn,
                    NameAr = seed.NameAr,
                    Code = seed.Code,
                    DisplayOrder = seed.DisplayOrder,
                    IsActive = true
                };
                _context.PaymentMethods.Add(method);
                byCode[seed.Code] = method;
                existing.Add(method);
            }

            await _context.SaveChangesAsync(ct);

            var enabled = (await _context.BranchPaymentMethods
                .Where(bpm => bpm.BranchId == RetailBranchId)
                .Select(bpm => bpm.PaymentMethodId)
                .ToListAsync(ct)).ToHashSet();

            foreach (var method in existing.Where(m => m.IsActive && !enabled.Contains(m.Id)))
            {
                _context.BranchPaymentMethods.Add(new BranchPaymentMethod
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = RetailBranchId,
                    PaymentMethodId = method.Id,
                    IsEnabled = true
                });
                report.PaymentMethods.Created++;
            }

            report.PaymentMethods.Unchanged += enabled.Count;
            await _context.SaveChangesAsync(ct);
        }

        // ── Master data ──────────────────────────────────────────────────────

        private async Task<Dictionary<string, Guid>> ApplySuppliersAsync(RetailSyncPlan plan, Guid tenantId, CancellationToken ct)
        {
            var resolved = new Dictionary<string, Guid>(StringComparer.Ordinal);

            foreach (var planned in plan.Suppliers)
            {
                if (planned.ExistingId is { } id)
                {
                    resolved[planned.Key] = id;
                    continue;
                }

                var supplier = new Supplier
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = RetailNaming.Truncate(planned.Name, 200),
                    Country = RetailNaming.Truncate(planned.Country, 80) is { Length: > 0 } country ? country : null
                };
                _context.Suppliers.Add(supplier);
                resolved[planned.Key] = supplier.Id;
            }

            await _context.SaveChangesAsync(ct);
            return resolved;
        }

        private async Task<Dictionary<string, Guid>> ApplyCategoriesAsync(RetailSyncPlan plan, Guid tenantId, CancellationToken ct)
        {
            var resolved = new Dictionary<string, Guid>(StringComparer.Ordinal);

            foreach (var planned in plan.Categories)
            {
                if (planned.ExistingId is { } id)
                {
                    resolved[planned.Key] = id;
                    continue;
                }

                var category = new Category
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = RetailNaming.Truncate(planned.Name, 100),
                    IsActive = true
                };
                _context.Categories.Add(category);
                resolved[planned.Key] = category.Id;
            }

            await _context.SaveChangesAsync(ct);
            return resolved;
        }

        // ── Products ─────────────────────────────────────────────────────────

        private async Task<Dictionary<string, AppliedProduct>> ApplyProductsAsync(
            RetailSyncPlan plan,
            Guid tenantId,
            IReadOnlyDictionary<string, Guid> suppliers,
            IReadOnlyDictionary<string, Guid> categories,
            CancellationToken ct)
        {
            var writable = plan.Products
                .Where(p => p.Action is RetailSyncAction.Create or RetailSyncAction.Update or RetailSyncAction.Unchanged)
                .ToList();

            var detailIds = writable.Where(p => p.Existing is not null).Select(p => p.Existing!.DetailId).ToList();
            var details = await _context.RetailProductDetails
                .Include(d => d.Product)
                .Where(d => detailIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d, ct);

            var resolved = new Dictionary<string, AppliedProduct>(StringComparer.Ordinal);

            foreach (var planned in writable)
            {
                var (product, detail) = planned.Existing is { } existing
                    ? (details[existing.DetailId].Product!, details[existing.DetailId])
                    : NewProduct(tenantId, planned.Row);

                ApplyProductFields(product, planned, categories);
                ApplyDetailFields(detail, planned, suppliers);
                resolved[planned.Row.SkuKey] = new AppliedProduct(product.Id, detail.Id, product.Name, product.CostPrice, product.BasePrice);

                if (planned.Existing is not null && product.PricingMode != ProductPricingModes.Manual)
                {
                    plan.Report.AddReview(RetailSheets.ProductMaster, planned.Row.Row,
                        $"SKU '{planned.Row.Sku}' is on '{product.PricingMode}' pricing, so the workbook's approved selling price may be recomputed by the pricing engine. Left as configured.");
                }
            }

            await _context.SaveChangesAsync(ct);
            return resolved;
        }

        /// <summary>What a product looks like after the workbook has been applied to it.
        /// Carried forward so the sale and stock phases never re-read the change tracker.</summary>
        private sealed record AppliedProduct(Guid ProductId, Guid DetailId, string Name, decimal? CostPrice, decimal BasePrice);

        private (Product Product, RetailProductDetail Detail) NewProduct(Guid tenantId, WorkbookProductRow row)
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = string.Empty,
                // Manual pricing: the approved selling price is the price of record and must
                // never be recomputed from cost by the automatic pricing modes.
                PricingMode = ProductPricingModes.Manual,
                Markup = 1,
                MarkupType = ProductMarkupTypes.Multiplier,
                IsActive = true
            };
            _context.Products.Add(product);

            var detail = new RetailProductDetail
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductId = product.Id,
                Sku = RetailNaming.Truncate(row.Sku, 64)
            };
            _context.RetailProductDetails.Add(detail);

            return (product, detail);
        }

        /// <summary>
        /// Workbook-owned product fields only. <c>IsActive</c>, pricing mode, kitchen routing,
        /// printers, Arabic text and every other operational setting are system-owned and are
        /// set once at creation — a spreadsheet does not get to reactivate a product an
        /// operator switched off.
        /// </summary>
        private static void ApplyProductFields(Product product, PlannedProduct planned, IReadOnlyDictionary<string, Guid> categories)
        {
            var row = planned.Row;
            product.Name = RetailNaming.Truncate(row.Name, 200);
            product.CostPrice = RetailRounding.Money(row.CostPerItem);
            // The workbook's APPROVED SELLING PRICE is the price the till charges.
            product.BasePrice = RetailRounding.Money(row.ApprovedSellingPrice);

            if (planned.CategoryKey is not null && categories.TryGetValue(planned.CategoryKey, out var categoryId))
                product.CategoryId = categoryId;
        }

        private static void ApplyDetailFields(RetailProductDetail detail, PlannedProduct planned, IReadOnlyDictionary<string, Guid> suppliers)
        {
            var row = planned.Row;
            detail.Barcode = RetailNaming.Truncate(row.Barcode, 64) is { Length: > 0 } barcode ? barcode : null;
            detail.Brand = RetailNaming.Truncate(row.Brand, 120) is { Length: > 0 } brand ? brand : null;
            detail.SizeLabel = RetailNaming.Truncate(row.Size, 40) is { Length: > 0 } size ? size : null;
            detail.ReceivedQuantity = RetailRounding.Money(row.ReceivedQuantity);
            detail.SupplierCostTotal = RetailRounding.Money(row.SupplierCostTotal);
            detail.ShippingCostPerUnit = RetailRounding.Shipping(row.ShippingCostPerUnit);
            detail.TargetMarginPercent = RetailRounding.Margin(row.TargetMarginFraction);

            if (row.Supplier is null || planned.SupplierKey is null || !suppliers.TryGetValue(planned.SupplierKey, out var supplierId))
                return;

            detail.SupplierId = supplierId;
            detail.CountryOfOrigin = RetailNaming.Truncate(RetailNaming.SplitSupplierAndCountry(row.Supplier).Country, 80) is { Length: > 0 } origin
                ? origin
                : detail.CountryOfOrigin;
        }

        /// <summary>
        /// Retail products are made available on the retail branch only. The cashier catalogue
        /// is filtered by BranchProduct/BranchCategory, so the restaurant POS never sees them.
        /// </summary>
        private async Task ApplyBranchAssignmentsAsync(
            RetailSyncPlan plan,
            Guid tenantId,
            IReadOnlyDictionary<string, AppliedProduct> products,
            IReadOnlyDictionary<string, Guid> categories,
            CancellationToken ct)
        {
            var productIds = products.Values.Select(p => p.ProductId).ToList();
            var assigned = (await _context.BranchProducts
                .Where(bp => bp.BranchId == RetailBranchId && productIds.Contains(bp.ProductId))
                .Select(bp => bp.ProductId)
                .ToListAsync(ct)).ToHashSet();

            var order = 0;
            foreach (var productId in productIds.Where(id => !assigned.Contains(id)))
            {
                _context.BranchProducts.Add(new BranchProduct
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = RetailBranchId,
                    ProductId = productId,
                    IsAvailable = true,
                    IsVisible = true,
                    DisplayOrder = ++order
                });
            }

            var categoryIds = plan.Products
                .Where(p => p.CategoryKey is not null && categories.ContainsKey(p.CategoryKey))
                .Select(p => categories[p.CategoryKey!])
                .Distinct()
                .ToList();

            var assignedCategories = (await _context.BranchCategories
                .Where(bc => bc.BranchId == RetailBranchId && categoryIds.Contains(bc.CategoryId))
                .Select(bc => bc.CategoryId)
                .ToListAsync(ct)).ToHashSet();

            var categoryOrder = 0;
            foreach (var categoryId in categoryIds.Where(id => !assignedCategories.Contains(id)))
            {
                _context.BranchCategories.Add(new BranchCategory
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = RetailBranchId,
                    CategoryId = categoryId,
                    IsVisible = true,
                    DisplayOrder = ++categoryOrder
                });
            }

            await _context.SaveChangesAsync(ct);
        }

        // ── Historical sales ─────────────────────────────────────────────────

        private async Task ApplySalesAsync(
            RetailSyncPlan plan,
            Guid tenantId,
            IReadOnlyDictionary<string, AppliedProduct> products,
            CancellationToken ct)
        {
            var methods = (await _context.PaymentMethods.ToListAsync(ct))
                .GroupBy(m => m.Code)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var updatedIds = plan.Sales
                .Where(s => s.Action == RetailSyncAction.Update && s.Existing is not null)
                .Select(s => s.Existing!.OrderId)
                .ToList();

            var tracked = await _context.Orders
                .Include(o => o.Payments)
                .Where(o => updatedIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, o => o, ct);

            var created = new List<(Guid OrderId, DateTime Date)>();

            foreach (var sale in plan.Sales)
            {
                if (sale.Action == RetailSyncAction.Create)
                {
                    var order = BuildOrder(sale, tenantId, products[sale.Row.SkuKey], methods);
                    created.Add((order.Id, sale.Row.Date!.Value));
                    continue;
                }

                if (sale.Action == RetailSyncAction.Update)
                    Converge(tracked[sale.Existing!.OrderId], sale, tenantId, methods);
            }

            await _context.SaveChangesAsync(ct);
            await BackdateAsync(created, ct);

            foreach (var (orderId, _) in created)
                await _orderStock.SyncOrderAsync(orderId, ct);

            plan.Report.OrdersReconciled = created.Count;
        }

        private Order BuildOrder(
            PlannedSale sale,
            Guid tenantId,
            AppliedProduct product,
            IReadOnlyDictionary<string, PaymentMethod> methods)
        {
            var row = sale.Row;
            var lineCost = decimal.Round((product.CostPrice ?? 0m) * row.Quantity, 2, MidpointRounding.AwayFromZero);

            var order = new Order
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BranchId = RetailBranchId,
                OrderNumber = sale.OrderNumber,
                DisplayOrderNumber = sale.OrderNumber,
                OrderType = OrderType.Takeaway,
                OrderSource = OrderSource.Pos,
                Status = sale.Settlement.Status,
                TableName = string.Empty,
                CustomerName = RetailNaming.Truncate(RetailNaming.MeaningfulOrNull(row.CustomerName), 120) is { Length: > 0 } name ? name : null,
                CustomerPhone = RetailNaming.Truncate(RetailNaming.PhoneOrNull(row.CustomerPhone), 20) is { Length: > 0 } phone ? phone : null,
                Subtotal = row.LineTotal,
                TotalAmount = row.LineTotal,
                TotalCost = lineCost,
                NetProfit = row.LineTotal - lineCost,
                PaymentMethod = sale.Settlement.MethodName,
                PaidAt = sale.Settlement.IsPaid ? row.Date : null
            };
            _context.Orders.Add(order);

            _context.OrderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BranchId = RetailBranchId,
                OrderId = order.Id,
                ProductId = product.ProductId,
                ProductName = product.Name,
                Quantity = row.Quantity,
                Price = RetailRounding.Money(row.SoldUnitPrice),
                UnitPriceSnapshot = RetailRounding.Money(row.ApprovedUnitPrice ?? product.BasePrice),
                LineTotalSnapshot = row.LineTotal,
                IsReady = true
            });

            AddPayment(order, sale.Settlement, row.LineTotal, tenantId, methods);
            return order;
        }

        /// <summary>
        /// Brings a sale imported earlier back in line with the workbook. Only workbook-owned
        /// fields move: the customer, the settlement and the order status. Quantity, price and
        /// date are the sale's identity and cannot change without it becoming a different sale.
        /// </summary>
        private void Converge(
            Order order,
            PlannedSale sale,
            Guid tenantId,
            IReadOnlyDictionary<string, PaymentMethod> methods)
        {
            var row = sale.Row;
            order.CustomerName = RetailNaming.Truncate(RetailNaming.MeaningfulOrNull(row.CustomerName), 120) is { Length: > 0 } name ? name : null;
            order.CustomerPhone = RetailNaming.Truncate(RetailNaming.PhoneOrNull(row.CustomerPhone), 20) is { Length: > 0 } phone ? phone : null;
            order.Status = sale.Settlement.Status;
            order.PaymentMethod = sale.Settlement.MethodName;

            if (!sale.Settlement.IsPaid)
                return;

            order.PaidAt ??= order.CreatedAt;

            if (order.Payments.Count == 0)
            {
                AddPayment(order, sale.Settlement, order.TotalAmount, tenantId, methods);
                return;
            }

            // The workbook changed how a settled sale was paid. Restate the method on the
            // single covering payment rather than deleting money and re-adding it.
            var payment = order.Payments.Count == 1 ? order.Payments.First() : null;
            if (payment is null || payment.Amount != order.TotalAmount)
                return;

            Restate(payment, sale.Settlement.MethodCode!, sale.Settlement.MethodName!, methods);
        }

        private static void Restate(
            Payment payment,
            string code,
            string name,
            IReadOnlyDictionary<string, PaymentMethod> methods)
        {
            methods.TryGetValue(code, out var method);
            payment.Method = name;
            payment.PaymentMethodId = method?.Id;
            payment.PaymentMethodName = method?.NameEn ?? name;
            payment.PaymentMethodNameAr = method?.NameAr ?? payment.PaymentMethodNameAr;
            payment.PaymentMethodCode = method?.Code ?? code;
        }

        /// <summary>Records the settlement. The payment-method link is optional: when the
        /// tenant has no matching method the payment is still recorded by name and code so the
        /// order is never left looking unpaid.</summary>
        private void AddPayment(
            Order order,
            RetailSettlement settlement,
            decimal amount,
            Guid tenantId,
            IReadOnlyDictionary<string, PaymentMethod> methods)
        {
            if (!settlement.IsPaid || amount <= 0m)
                return;

            methods.TryGetValue(settlement.MethodCode!, out var method);

            _context.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BranchId = RetailBranchId,
                OrderId = order.Id,
                Amount = amount,
                Method = settlement.MethodName!,
                PaymentMethodId = method?.Id,
                PaymentMethodName = method?.NameEn ?? settlement.MethodName,
                PaymentMethodCode = method?.Code ?? settlement.MethodCode
            });
        }

        /// <summary>
        /// PosDbContext stamps CreatedAt with the current time for every inserted BaseEntity,
        /// so imported history would otherwise all land on the import date. The audit stamp is
        /// shared behaviour and is not modified; the imported rows are corrected afterwards.
        /// </summary>
        private async Task BackdateAsync(IReadOnlyList<(Guid OrderId, DateTime Date)> orders, CancellationToken ct)
        {
            foreach (var (orderId, date) in orders)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"Orders\" SET \"CreatedAt\" = {date}, \"UpdatedAt\" = {date} WHERE \"Id\" = {orderId}", ct);
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"OrderItems\" SET \"CreatedAt\" = {date}, \"UpdatedAt\" = {date} WHERE \"OrderId\" = {orderId}", ct);
            }
        }

        // ── Historical payment normalisation ─────────────────────────────────

        /// <summary>
        /// Brings every workbook-imported retail payment in line with the confirmed settlement
        /// ruling. Metadata only: it settles a completed sale that has none, restates a method
        /// nothing can act on, and moves a payment to the date the sale happened. It never
        /// changes an amount, never touches stock, never settles an unpaid order, and is
        /// scoped by order number prefix AND branch so no operational or Main-branch payment
        /// is in range.
        /// </summary>
        private async Task ApplyPaymentNormalisationAsync(RetailSyncPlan plan, Guid tenantId, CancellationToken ct)
        {
            var methods = (await _context.PaymentMethods.ToListAsync(ct))
                .GroupBy(m => m.Code)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var settlements = plan.PaymentFixes.Where(f => f.Kind == RetailPaymentFixKind.MissingSettlement).ToList();
            var restatements = plan.PaymentFixes.Where(f => f.Kind == RetailPaymentFixKind.UnreliableMethod).ToList();

            if (settlements.Count > 0)
            {
                var orderIds = settlements.Select(f => f.OrderId).ToList();
                var orders = await _context.Orders
                    .Include(o => o.Payments)
                    .Where(o => orderIds.Contains(o.Id))
                    .ToListAsync(ct);

                foreach (var order in orders.Where(o => o.Payments.Count == 0 && o.TotalAmount > 0m))
                {
                    order.PaymentMethod = RetailPaymentMapping.CashName;
                    order.PaidAt ??= order.CreatedAt;
                    AddPayment(order, CashSettlement, order.TotalAmount, tenantId, methods);
                    plan.Report.PaymentsSettled++;
                }
            }

            if (restatements.Count > 0)
            {
                var paymentIds = restatements.Select(f => f.PaymentId!.Value).ToList();
                var payments = await _context.Payments.Where(p => paymentIds.Contains(p.Id)).ToListAsync(ct);

                foreach (var payment in payments)
                {
                    Restate(payment, RetailPaymentMapping.CashCode, RetailPaymentMapping.CashName, methods);
                    plan.Report.PaymentMethodsRestated++;
                }
            }

            await _context.SaveChangesAsync(ct);
            plan.Report.PaymentDatesCorrected = await AlignPaymentDatesAsync(ct);
        }

        private static RetailSettlement CashSettlement => new(
            OrderStatus.Completed, true, RetailPaymentMapping.CashCode, RetailPaymentMapping.CashName, false, null);

        /// <summary>
        /// A historical payment's transaction date is the date of the sale, never the date the
        /// importer happened to run — <c>PosDbContext</c> stamps the audit columns on insert,
        /// so they are corrected here in one set-based statement. This is the single place any
        /// workbook payment gets its date, which is what stops the import clock leaking back in.
        ///
        /// Idempotent by construction: it only matches rows that are wrong, so a second run
        /// updates nothing. Scoped to the retail branch's DL- orders, so operational and
        /// restaurant payments can never be in range.
        /// </summary>
        private Task<int> AlignPaymentDatesAsync(CancellationToken ct)
        {
            var prefix = $"{OrderNumberPrefix}%";
            return _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 UPDATE "Payments" p
                 SET "CreatedAt" = o."CreatedAt", "UpdatedAt" = o."CreatedAt"
                 FROM "Orders" o
                 WHERE o."Id" = p."OrderId"
                   AND o."BranchId" = {RetailBranchId}
                   AND o."OrderNumber" LIKE {prefix}
                   AND p."CreatedAt" <> o."CreatedAt"
                 """, ct);
        }

        // ── Historical purchase orders (header-only) ─────────────────────────

        /// <summary>
        /// Applies the PURCHASE ORDER sheet as header-only history. The sheet has no SKU-level
        /// lines, so none are invented and no stock movement or COGS is produced.
        /// </summary>
        private async Task ApplyPurchasesAsync(
            RetailSyncPlan plan,
            Guid tenantId,
            IReadOnlyDictionary<string, Guid> suppliers,
            CancellationToken ct)
        {
            var ids = plan.Purchases.Where(p => p.Existing is not null).Select(p => p.Existing!.Id).ToList();
            var tracked = await _context.RetailLegacyPurchases
                .Where(p => ids.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p, ct);

            foreach (var planned in plan.Purchases)
            {
                if (planned.Action is RetailSyncAction.Skip or RetailSyncAction.Unchanged)
                    continue;

                var purchase = planned.Existing is { } existing
                    ? tracked[existing.Id]
                    : NewPurchase(tenantId, planned.Row.Key);

                ApplyPurchaseFields(purchase, planned, suppliers);
            }

            await _context.SaveChangesAsync(ct);
        }

        private RetailLegacyPurchase NewPurchase(Guid tenantId, string key)
        {
            var purchase = new RetailLegacyPurchase
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BranchId = RetailBranchId,
                PurchaseOrderNumber = key
            };
            _context.RetailLegacyPurchases.Add(purchase);
            return purchase;
        }

        private static void ApplyPurchaseFields(
            RetailLegacyPurchase purchase,
            PlannedPurchase planned,
            IReadOnlyDictionary<string, Guid> suppliers)
        {
            var row = planned.Row;
            purchase.OrderDateUtc = row.Date!.Value;
            purchase.InvoiceNumber = RetailNaming.Truncate(row.InvoiceNumber, 50) is { Length: > 0 } invoice ? invoice : null;
            purchase.QuantityPieces = RetailRounding.Money(row.Quantity);
            purchase.SupplierCost = RetailRounding.Money(row.SupplierCost);
            purchase.ShippingCustomsClearance = RetailRounding.Money(row.ShippingCustomsClearance);
            // Recomputed rather than read from the sheet's TOTAL COST OF PO column.
            purchase.TotalCost = purchase.SupplierCost + purchase.ShippingCustomsClearance;
            purchase.Status = RetailNaming.Truncate(row.Status, 40) is { Length: > 0 } status ? status : null;
            purchase.SourceRowNumber = row.Row;

            if (row.Supplier is null)
                return;

            purchase.SupplierNameSnapshot = RetailNaming.Truncate(row.Supplier, 200);
            if (planned.SupplierKey is not null && suppliers.TryGetValue(planned.SupplierKey, out var supplierId))
                purchase.SupplierId = supplierId;
        }

        // ── Stock ────────────────────────────────────────────────────────────

        /// <summary>
        /// Posts the workbook's contribution to the ledger: an opening position for a product
        /// that has none, and a signed correction when the workbook's received quantity has
        /// moved since the last sync. Existing movements are never edited or deleted — a
        /// changed quantity is corrected by a compensating movement, so the audit trail keeps
        /// both the original figure and the reason it changed.
        /// </summary>
        private async Task ApplyStockAsync(
            RetailSyncPlan plan,
            IReadOnlyDictionary<string, AppliedProduct> products,
            CancellationToken ct)
        {
            if (plan.Stock.Count == 0)
                return;

            var openingAt = await ResolveOpeningTimestampAsync(ct);
            var correctedAt = DateTime.UtcNow;

            var postings = plan.Stock
                .Where(s => products.ContainsKey(s.SkuKey))
                .Select(s =>
                {
                    var product = products[s.SkuKey];
                    var isOpening = s.MovementType == RetailStockMovementType.OpeningBalance;

                    return new RetailStockPosting(
                        ProductId: product.ProductId,
                        BranchId: RetailBranchId,
                        MovementType: s.MovementType,
                        Quantity: s.Quantity,
                        UnitCost: s.UnitCost,
                        SourceDocumentType: RetailStockSourceDocument.WorkbookImport,
                        SourceDocumentId: product.DetailId,
                        // Only the opening balance is keyed on a source line; it has its own
                        // unique index. A correction carries none, because its exactly-once
                        // guarantee is the reconciled difference itself — a repeat run
                        // computes zero and posts nothing.
                        SourceLineId: isOpening ? product.DetailId : null,
                        SourceReference: RetailNaming.Truncate(s.Sku, 80),
                        OccurredAtUtc: isOpening ? openingAt : correctedAt,
                        PerformedBySystem: PerformedBySystem,
                        Notes: RetailNaming.Truncate(s.Reason, 300));
                })
                .ToList();

            await _ledger.PostManyAsync(postings, ct);
        }

        /// <summary>The opening position predates every recorded sale, so it is stamped a day
        /// before the first one.</summary>
        private async Task<DateTime> ResolveOpeningTimestampAsync(CancellationToken ct)
        {
            var firstSale = await _context.Orders
                .AsNoTracking()
                .Where(o => o.BranchId == RetailBranchId)
                .MinAsync(o => (DateTime?)o.CreatedAt, ct);

            var stamp = (firstSale ?? DateTime.UtcNow).AddDays(-1);
            return DateTime.SpecifyKind(stamp, DateTimeKind.Utc);
        }

        private async Task MeasureAsync(RetailWorkbookImportResult report, CancellationToken ct)
        {
            var movements = await _context.RetailStockMovements
                .AsNoTracking()
                .Select(m => new { m.Quantity })
                .ToListAsync(ct);

            report.AppliedOnHand = movements.Sum(m => m.Quantity);
            report.AppliedMovements = movements.Count - report.PreSyncMovements;
        }
    }
}
