using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Retail.Domain;

namespace RestaurantPos.Api.Modules.Retail.Import
{
    /// <summary>Resolved intent for one workbook product row, carried from the plan into the
    /// apply phase so the decision is made exactly once.</summary>
    internal sealed record PlannedProduct(
        WorkbookProductRow Row,
        RetailSyncAction Action,
        SnapshotProduct? Existing,
        string? SupplierKey,
        string? CategoryKey,
        IReadOnlyList<RetailFieldChange> Changes);

    internal sealed record PlannedSale(
        WorkbookSaleRow Row,
        RetailSyncAction Action,
        SnapshotSale? Existing,
        string OrderNumber,
        RetailSettlement Settlement,
        IReadOnlyList<RetailFieldChange> Changes,
        string? Reason);

    internal sealed record PlannedPurchase(
        WorkbookPurchaseRow Row,
        RetailSyncAction Action,
        SnapshotPurchase? Existing,
        string? SupplierKey,
        IReadOnlyList<RetailFieldChange> Changes,
        string? Reason);

    internal sealed record PlannedSupplier(
        string Key,
        string WorkbookText,
        string Name,
        string? Country,
        RetailSyncAction Action,
        Guid? ExistingId);

    internal sealed record PlannedCategory(string Key, string Name, RetailSyncAction Action, Guid? ExistingId);

    /// <summary>A historical payment the confirmed ruling requires the sync to put right.
    /// <paramref name="PaymentId"/> is null when the payment does not exist yet.</summary>
    internal sealed record PlannedPaymentFix(
        Guid OrderId,
        string OrderNumber,
        Guid? PaymentId,
        RetailPaymentFixKind Kind,
        decimal Amount,
        DateTime TransactionDateUtc,
        string Detail);

    internal sealed record PlannedStock(
        string SkuKey,
        string Sku,
        RetailStockMovementType MovementType,
        decimal Quantity,
        decimal? UnitCost,
        string Reason);

    /// <summary>The complete decision set, plus the report the caller prints.</summary>
    internal sealed class RetailSyncPlan
    {
        public required RetailWorkbookImportResult Report { get; init; }
        public required IReadOnlyList<PlannedSupplier> Suppliers { get; init; }
        public required IReadOnlyList<PlannedCategory> Categories { get; init; }
        public required IReadOnlyList<PlannedProduct> Products { get; init; }
        public required IReadOnlyList<PlannedSale> Sales { get; init; }
        public required IReadOnlyList<PlannedPurchase> Purchases { get; init; }
        public required IReadOnlyList<PlannedStock> Stock { get; init; }
        public required IReadOnlyList<PlannedPaymentFix> PaymentFixes { get; init; }
    }

    /// <summary>
    /// Decides everything the sync will do, from the workbook and a read-only database
    /// snapshot. Writes nothing and holds no <c>DbContext</c> — which is what makes
    /// <c>--dry-run</c> a genuine no-write mode rather than a transaction that is rolled back.
    /// </summary>
    internal sealed class RetailWorkbookPlanner
    {
        private const int MinimumAliasLength = 6;
        private const string OrderNumberPrefix = "DL-";

        private readonly RetailWorkbookReader _reader;
        private readonly RetailWorkbookSnapshot _snapshot;
        private readonly RetailWorkbookImportResult _report;

        public RetailWorkbookPlanner(
            RetailWorkbookReader reader,
            RetailWorkbookSnapshot snapshot,
            RetailWorkbookImportResult report)
        {
            _reader = reader;
            _snapshot = snapshot;
            _report = report;
        }

        public RetailSyncPlan Build()
        {
            RecordSheets();

            var productRows = _reader.ReadProducts();
            var saleRows = _reader.ReadSales();
            var purchaseRows = _reader.ReadPurchases();

            var suppliers = PlanSuppliers(productRows, purchaseRows);
            var categories = PlanCategories(productRows);
            var products = PlanProducts(productRows, suppliers, categories);
            var sales = PlanSales(saleRows, products);
            var purchases = PlanPurchases(purchaseRows, suppliers);
            var stock = PlanStock(products);
            var paymentFixes = PlanPaymentFixes(sales);

            Publish(products, sales, stock);
            VerifyInventory(products, sales, stock);
            VerifyProfit(sales, products);
            SummarisePayments(sales);

            return new RetailSyncPlan
            {
                Report = _report,
                Suppliers = suppliers,
                Categories = categories,
                Products = products,
                Sales = sales,
                Purchases = purchases,
                Stock = stock,
                PaymentFixes = paymentFixes
            };
        }

        // ── Sheets ───────────────────────────────────────────────────────────

        private void RecordSheets()
        {
            _report.SheetsRead.AddRange(_reader.SheetNames);

            foreach (var expected in new[]
                     {
                         RetailSheets.ProductMaster, RetailSheets.Sellout, RetailSheets.PurchaseOrders,
                         RetailSheets.LiveInventory, RetailSheets.SummaryProfit, RetailSheets.Suppliers
                     })
            {
                if (!_reader.HasSheet(expected))
                    _report.SheetsMissing.Add(expected);
            }

            if (!_reader.HasSheet(RetailSheets.Suppliers))
            {
                _report.AddReview(RetailSheets.Suppliers, 0,
                    "Sheet absent from this revision. Existing supplier contact details are left untouched — "
                    + "no supplier is deleted and no contact field is blanked.");
            }

            Record(RetailSheets.ProductMaster, ProductHeaders.NotImported);
            Record(RetailSheets.Sellout, SalesHeaders.NotImported);
            Record(RetailSheets.PurchaseOrders, PurchaseOrderHeaders.NotImported);
            Record(RetailSheets.PurchaseOrders, new[] { PurchaseOrderHeaders.ForeignCurrencyCost });

            void Record(string sheet, IReadOnlyList<string> headers)
            {
                foreach (var header in _reader.UnimportedHeaders(sheet, headers))
                    _report.ColumnsNotImported.Add($"[{sheet}] {header}");
            }
        }

        // ── Suppliers ────────────────────────────────────────────────────────

        private IReadOnlyList<PlannedSupplier> PlanSuppliers(
            IReadOnlyList<WorkbookProductRow> products,
            IReadOnlyList<WorkbookPurchaseRow> purchases)
        {
            var referenced = products
                .Where(p => p.Supplier is not null)
                .Select(p => (Raw: p.Supplier!, IsProduct: true))
                .Concat(purchases.Where(p => p.Supplier is not null).Select(p => (Raw: p.Supplier!, IsProduct: false)))
                .ToList();

            var planned = new Dictionary<string, PlannedSupplier>();
            var productCounts = new Dictionary<string, int>();
            var purchaseCounts = new Dictionary<string, int>();

            foreach (var (raw, isProduct) in referenced)
            {
                var (name, country) = RetailNaming.SplitSupplierAndCountry(raw);
                var key = RetailNaming.Key(name);

                if (!planned.ContainsKey(key))
                    planned[key] = Resolve(raw, name, country, key);

                var counts = isProduct ? productCounts : purchaseCounts;
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }

            foreach (var supplier in planned.Values)
            {
                _report.Suppliers.Add(new RetailSupplierPlan(
                    supplier.WorkbookText, supplier.Name, supplier.Country, supplier.Action, supplier.ExistingId,
                    supplier.ExistingId is null ? null : _snapshot.SuppliersByKey.Values.FirstOrDefault(s => s.Id == supplier.ExistingId)?.Name,
                    productCounts.GetValueOrDefault(supplier.Key),
                    purchaseCounts.GetValueOrDefault(supplier.Key)));
            }

            return planned.Values.ToList();
        }

        /// <summary>
        /// Matches the workbook's free-text supplier against the one supplier master. An exact
        /// key wins; failing that a containment alias ("SILVER STAR" ↔ "SILVER STAR TRADING")
        /// reuses the existing record and is reported, because a spelling drift must never
        /// create a second supplier.
        /// </summary>
        private PlannedSupplier Resolve(string raw, string name, string? country, string key)
        {
            if (_snapshot.SuppliersByKey.TryGetValue(key, out var exact))
                return new PlannedSupplier(key, raw, exact.Name, country ?? exact.Country, RetailSyncAction.Unchanged, exact.Id);

            var alias = _snapshot.SuppliersByKey
                .Where(s => IsAlias(s.Key, key))
                .Select(s => s.Value)
                .FirstOrDefault();

            if (alias is not null)
            {
                _report.AddReview(RetailSheets.ProductMaster, 0,
                    $"Supplier '{raw}' matched the existing supplier '{alias.Name}' by name containment — no new supplier was created.");
                return new PlannedSupplier(key, raw, alias.Name, country ?? alias.Country, RetailSyncAction.Unchanged, alias.Id);
            }

            return new PlannedSupplier(key, raw, name, country, RetailSyncAction.Create, null);
        }

        private static bool IsAlias(string existing, string candidate)
            => Math.Min(existing.Length, candidate.Length) >= MinimumAliasLength
               && (existing.StartsWith(candidate, StringComparison.Ordinal)
                   || candidate.StartsWith(existing, StringComparison.Ordinal));

        // ── Categories ───────────────────────────────────────────────────────

        private IReadOnlyList<PlannedCategory> PlanCategories(IReadOnlyList<WorkbookProductRow> rows)
        {
            var planned = new Dictionary<string, PlannedCategory>();
            var counts = new Dictionary<string, int>();

            foreach (var name in rows.Select(r => r.Category).OfType<string>())
            {
                var key = RetailNaming.Key(name);
                counts[key] = counts.GetValueOrDefault(key) + 1;

                if (planned.ContainsKey(key))
                    continue;

                planned[key] = _snapshot.CategoriesByKey.TryGetValue(key, out var existing)
                    ? new PlannedCategory(key, existing.Name, RetailSyncAction.Unchanged, existing.Id)
                    : new PlannedCategory(key, name, RetailSyncAction.Create, null);
            }

            foreach (var category in planned.Values)
                _report.Categories.Add(new RetailCategoryPlan(category.Name, category.Action, category.ExistingId, counts[category.Key]));

            return planned.Values.ToList();
        }

        // ── Products ─────────────────────────────────────────────────────────

        private IReadOnlyList<PlannedProduct> PlanProducts(
            IReadOnlyList<WorkbookProductRow> rows,
            IReadOnlyList<PlannedSupplier> suppliers,
            IReadOnlyList<PlannedCategory> categories)
        {
            var supplierByKey = suppliers.ToDictionary(s => s.Key);
            var categoryByKey = categories.ToDictionary(c => c.Key);
            var planned = new List<PlannedProduct>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var row in rows)
            {
                if (row.Name is null)
                {
                    planned.Add(Refused(row, RetailSyncAction.Skip, $"SKU '{row.Sku}' has no product name."));
                    continue;
                }

                if (!seen.Add(row.SkuKey))
                {
                    planned.Add(Refused(row, RetailSyncAction.Skip, $"SKU '{row.Sku}' appears more than once in PRODUCT MASTER — first occurrence kept."));
                    continue;
                }

                _snapshot.ProductsBySkuKey.TryGetValue(row.SkuKey, out var existing);

                if (BarcodeConflict(row, existing) is { } conflict)
                {
                    planned.Add(Refused(row, RetailSyncAction.Conflict, conflict));
                    continue;
                }

                var supplierKey = row.Supplier is null ? null : RetailNaming.Key(RetailNaming.SplitSupplierAndCountry(row.Supplier).Name);
                var categoryKey = row.Category is null ? null : RetailNaming.Key(row.Category);

                var changes = existing is null
                    ? Array.Empty<RetailFieldChange>()
                    : Differences(row, existing, supplierKey, categoryKey, supplierByKey, categoryByKey);

                var action = existing is null
                    ? RetailSyncAction.Create
                    : changes.Count > 0 || !existing.AssignedToRetailBranch
                        ? RetailSyncAction.Update
                        : RetailSyncAction.Unchanged;

                planned.Add(new PlannedProduct(row, action, existing, supplierKey, categoryKey, changes));

                if (row.ApprovedSellingPrice <= 0m)
                    _report.AddIssue(RetailSheets.ProductMaster, row.Row, $"'{row.Name}' has no approved selling price — imported at 0.");
            }

            foreach (var product in planned)
            {
                _report.Products.Add(new RetailProductPlan(
                    product.Row.Row, product.Row.Sku, product.Row.Name, product.Action,
                    product.Existing?.ProductId, product.Changes,
                    product.Action is RetailSyncAction.Skip or RetailSyncAction.Conflict
                        ? product.Changes.FirstOrDefault()?.To
                        : null));
            }

            return planned;
        }

        private PlannedProduct Refused(WorkbookProductRow row, RetailSyncAction action, string reason)
        {
            _report.AddIssue(RetailSheets.ProductMaster, row.Row, reason);
            if (action == RetailSyncAction.Conflict)
                _report.AddReview(RetailSheets.ProductMaster, row.Row, reason);

            return new PlannedProduct(row, action, null, null, null, new[] { new RetailFieldChange("reason", null, reason) });
        }

        /// <summary>The barcode is secondary evidence, never permission to write. When it
        /// already belongs to a different SKU the row is refused rather than guessed at.</summary>
        private string? BarcodeConflict(WorkbookProductRow row, SnapshotProduct? existing)
        {
            if (row.Barcode is null)
                return null;

            var key = RetailNaming.SkuKey(row.Barcode);
            if (!_snapshot.ProductsByBarcodeKey.TryGetValue(key, out var owners))
                return null;

            var clashing = owners.Where(o => RetailNaming.SkuKey(o.Sku) != row.SkuKey).ToList();
            if (clashing.Count == 0)
                return null;

            return existing is null
                ? $"Barcode '{row.Barcode}' already belongs to SKU '{clashing[0].Sku}'. SKU '{row.Sku}' is not in the database, so this row would create a second product sharing one barcode."
                : $"Barcode '{row.Barcode}' is recorded against SKU '{clashing[0].Sku}' as well as '{row.Sku}'.";
        }

        private static IReadOnlyList<RetailFieldChange> Differences(
            WorkbookProductRow row,
            SnapshotProduct existing,
            string? supplierKey,
            string? categoryKey,
            IReadOnlyDictionary<string, PlannedSupplier> suppliers,
            IReadOnlyDictionary<string, PlannedCategory> categories)
        {
            var changes = new List<RetailFieldChange>();

            Compare("name", existing.Name, row.Name);
            Compare("cost price", existing.CostPrice, RetailRounding.Money(row.CostPerItem));
            Compare("approved selling price", existing.BasePrice, RetailRounding.Money(row.ApprovedSellingPrice));
            Compare("barcode", existing.Barcode, row.Barcode);
            Compare("brand", existing.Brand, row.Brand);
            Compare("size", existing.SizeLabel, row.Size);
            Compare("target margin %", existing.TargetMarginPercent, RetailRounding.Margin(row.TargetMarginFraction));
            Compare("received quantity", existing.ReceivedQuantity, RetailRounding.Money(row.ReceivedQuantity));
            Compare("supplier cost total", existing.SupplierCostTotal, RetailRounding.Money(row.SupplierCostTotal));
            Compare("shipping per unit", existing.ShippingCostPerUnit, RetailRounding.Shipping(row.ShippingCostPerUnit));

            if (row.Supplier is not null)
            {
                Compare("country of origin", existing.CountryOfOrigin,
                    RetailNaming.SplitSupplierAndCountry(row.Supplier).Country ?? suppliers.GetValueOrDefault(supplierKey!)?.Country);

                var supplierId = supplierKey is null ? null : suppliers.GetValueOrDefault(supplierKey)?.ExistingId;
                if (supplierId != existing.SupplierId)
                    changes.Add(new RetailFieldChange("supplier", existing.SupplierId?.ToString(), suppliers.GetValueOrDefault(supplierKey!)?.Name));
            }

            if (categoryKey is not null)
            {
                var categoryId = categories.GetValueOrDefault(categoryKey)?.ExistingId;
                if (categoryId != existing.CategoryId)
                    changes.Add(new RetailFieldChange("category", existing.CategoryId?.ToString(), categories.GetValueOrDefault(categoryKey)?.Name));
            }

            return changes;

            void Compare<T>(string field, T from, T to)
            {
                if (!EqualityComparer<T>.Default.Equals(from, to))
                    changes.Add(new RetailFieldChange(field, from?.ToString(), to?.ToString()));
            }
        }

        // ── Historical sales ─────────────────────────────────────────────────

        /// <summary>
        /// Matches every workbook sale row to a sale already imported, or plans a new one.
        ///
        /// Identity is the transaction itself — date, SKU, quantity and sold unit price —
        /// with an occurrence ordinal, NOT the workbook row number the previous importer used.
        /// The 2026-09 revision overwrites two rows in place and appends 97 more, so a
        /// row-numbered key would both miss the rewritten rows and re-import moved ones.
        ///
        /// Two passes: rows that still match on customer and settlement claim their order
        /// first, so a re-run cannot shuffle two otherwise identical sales between each other
        /// and report work that is not there.
        /// </summary>
        private IReadOnlyList<PlannedSale> PlanSales(
            IReadOnlyList<WorkbookSaleRow> rows,
            IReadOnlyList<PlannedProduct> products)
        {
            var sellable = products
                .Where(p => p.Action != RetailSyncAction.Skip && p.Action != RetailSyncAction.Conflict)
                .Select(p => p.Row.SkuKey)
                .ToHashSet(StringComparer.Ordinal);

            var candidates = _snapshot.Sales
                .GroupBy(CoreKey)
                .ToDictionary(g => g.Key, g => g.ToList());

            var claimed = new HashSet<Guid>();
            var matches = new Dictionary<int, SnapshotSale>();
            var usable = new List<WorkbookSaleRow>();
            var planned = new List<PlannedSale>();

            foreach (var row in rows.OrderBy(r => r.Row))
            {
                if (Reject(row, sellable) is { } reason)
                {
                    planned.Add(new PlannedSale(row, RetailSyncAction.Skip, null, string.Empty,
                        RetailPaymentMapping.Resolve(row.Payment, row.LineTotal), Array.Empty<RetailFieldChange>(), reason));
                    _report.AddIssue(RetailSheets.Sellout, row.Row, reason);
                    continue;
                }

                usable.Add(row);
            }

            foreach (var row in usable)
                Claim(row, exact: true);

            foreach (var row in usable.Where(r => !matches.ContainsKey(r.Row)))
                Claim(row, exact: false);

            var allocated = new HashSet<string>(_snapshot.UsedOrderNumbers, StringComparer.OrdinalIgnoreCase);

            foreach (var row in usable)
            {
                var settlement = RetailPaymentMapping.Resolve(row.Payment, row.LineTotal);
                if (settlement.IsUnknown)
                    _report.AddIssue(RetailSheets.Sellout, row.Row, $"Unrecognised payment value '{settlement.RawValue}' — order recorded as unpaid.");

                if (matches.TryGetValue(row.Row, out var existing))
                {
                    var (action, changes, reason) = Converge(row, existing, settlement);
                    planned.Add(new PlannedSale(row, action, existing, existing.OrderNumber, settlement, changes, reason));
                    continue;
                }

                planned.Add(new PlannedSale(row, RetailSyncAction.Create, null, Allocate(row.Row, allocated),
                    settlement, Array.Empty<RetailFieldChange>(), null));
            }

            ReportOrphans(claimed);
            foreach (var sale in planned)
            {
                _report.Sales.Add(new RetailSalePlan(
                    sale.Row.Row, sale.Row.Sku, sale.Row.Date, sale.Row.Quantity, sale.Row.SoldUnitPrice,
                    sale.Row.LineTotal, sale.Action, sale.OrderNumber, sale.Existing?.OrderId, sale.Changes, sale.Reason));
            }

            return planned;

            void Claim(WorkbookSaleRow row, bool exact)
            {
                if (!candidates.TryGetValue(CoreKey(row), out var pool))
                    return;

                foreach (var candidate in pool)
                {
                    if (claimed.Contains(candidate.OrderId))
                        continue;
                    if (exact && !IsExactMatch(row, candidate))
                        continue;

                    claimed.Add(candidate.OrderId);
                    matches[row.Row] = candidate;
                    return;
                }
            }
        }

        private string? Reject(WorkbookSaleRow row, IReadOnlySet<string> sellable)
        {
            if (row.Date is null)
                return $"SKU '{row.Sku}' has no sale date.";
            if (row.Quantity <= 0)
                return $"SKU '{row.Sku}' has a quantity of {row.Quantity}.";
            if (!sellable.Contains(row.SkuKey))
                return $"SKU '{row.Sku}' is not a usable PRODUCT MASTER row.";
            return null;
        }

        private static string CoreKey(WorkbookSaleRow row)
            => $"{row.Date:yyyy-MM-dd}|{row.SkuKey}|{row.Quantity}|{RetailRounding.Money(row.SoldUnitPrice):0.00}";

        private static string CoreKey(SnapshotSale sale)
            => $"{sale.OccurredOn:yyyy-MM-dd}|{sale.SkuKey}|{sale.Quantity}|{RetailRounding.Money(sale.UnitPrice):0.00}";

        private static bool IsExactMatch(WorkbookSaleRow row, SnapshotSale sale)
        {
            var settlement = RetailPaymentMapping.Resolve(row.Payment, row.LineTotal);
            return RetailNaming.MeaningfulOrNull(row.CustomerName) == sale.CustomerName
                   && settlement.MethodName == sale.PaymentMethod
                   && settlement.Status == sale.Status;
        }

        /// <summary>
        /// Workbook-owned changes to a sale already imported. Money already recorded is never
        /// removed: a row that has gone from a settled method back to "NOT PAID" is reported
        /// for a person to resolve, because deleting a Payment is not a spreadsheet's decision.
        /// </summary>
        private (RetailSyncAction Action, IReadOnlyList<RetailFieldChange> Changes, string? Reason) Converge(
            WorkbookSaleRow row,
            SnapshotSale existing,
            RetailSettlement settlement)
        {
            var changes = new List<RetailFieldChange>();
            var name = RetailNaming.MeaningfulOrNull(row.CustomerName);
            var phone = RetailNaming.PhoneOrNull(row.CustomerPhone);

            if (name != existing.CustomerName)
                changes.Add(new RetailFieldChange("customer name", existing.CustomerName, name));
            if (phone != existing.CustomerPhone)
                changes.Add(new RetailFieldChange("customer phone", existing.CustomerPhone, phone));

            if (existing.PaymentCount > 0 && !settlement.IsPaid)
            {
                var reason = $"Workbook now reads '{settlement.RawValue ?? "blank"}' but {existing.OrderNumber} already has a recorded payment of {existing.TotalAmount:0.00}. "
                             + "The payment is left in place — removing settled money is not a workbook decision.";
                _report.AddReview(RetailSheets.Sellout, row.Row, reason);
                return (RetailSyncAction.Conflict, changes, reason);
            }

            if (settlement.Status != existing.Status)
                changes.Add(new RetailFieldChange("status", existing.Status.ToString(), settlement.Status.ToString()));
            if (settlement.MethodName != existing.PaymentMethod)
                changes.Add(new RetailFieldChange("payment method", existing.PaymentMethod, settlement.MethodName));
            if (settlement.IsPaid && existing.PaymentCount == 0)
                changes.Add(new RetailFieldChange("settlement", "none recorded", $"{settlement.MethodName} {row.LineTotal:0.00}"));

            return (changes.Count == 0 ? RetailSyncAction.Unchanged : RetailSyncAction.Update, changes, null);
        }

        private void ReportOrphans(IReadOnlySet<Guid> claimed)
        {
            foreach (var sale in _snapshot.Sales.Where(s => !claimed.Contains(s.OrderId)))
            {
                _report.OrphanSales.Add(new RetailOrphanSale(sale.OrderNumber, sale.SkuKey, sale.OccurredOn, sale.TotalAmount));
                _report.AddReview(RetailSheets.Sellout, 0,
                    $"{sale.OrderNumber} ({sale.OccurredOn:yyyy-MM-dd}, {sale.SkuKey}, {sale.TotalAmount:0.00}) exists in the database but has no row in this workbook revision. "
                    + "Kept as recorded history; it is why the ledger and LIVE INVENTORY differ for that SKU.");
            }
        }

        /// <summary>Order numbers stay row-derived so an imported sale still points back at its
        /// workbook line, and gain a suffix when that number is already taken — identity comes
        /// from the transaction, not from this label.</summary>
        private static string Allocate(int row, ISet<string> allocated)
        {
            var candidate = $"{OrderNumberPrefix}{row:0000}";
            var number = candidate;
            var suffix = 1;

            while (!allocated.Add(number))
                number = $"{candidate}-{++suffix}";

            return number;
        }

        // ── Legacy purchase orders ───────────────────────────────────────────

        private IReadOnlyList<PlannedPurchase> PlanPurchases(
            IReadOnlyList<WorkbookPurchaseRow> rows,
            IReadOnlyList<PlannedSupplier> suppliers)
        {
            var supplierByKey = suppliers.ToDictionary(s => s.Key);
            var planned = new List<PlannedPurchase>();

            foreach (var row in rows)
            {
                if (row.Date is null)
                {
                    planned.Add(new PlannedPurchase(row, RetailSyncAction.Skip, null, null,
                        Array.Empty<RetailFieldChange>(), $"PO '{row.Number}' has no date."));
                    _report.AddIssue(RetailSheets.PurchaseOrders, row.Row, $"PO '{row.Number}' has no date — not imported.");
                    continue;
                }

                if (!string.Equals(row.Key, row.Number, StringComparison.Ordinal))
                {
                    _report.AddReview(RetailSheets.PurchaseOrders, row.Row,
                        $"PO NUMBER '{row.Number}' is used by more than one row in this sheet, so it cannot be the historical identity on its own. "
                        + $"This row is keyed '{row.Key}' using its invoice reference.");
                }

                if (row.ForeignCurrencyCost.HasValue)
                {
                    _report.AddReview(RetailSheets.PurchaseOrders, row.Row,
                        $"PO '{row.Key}' carries a supplier cost of {row.ForeignCurrencyCost:0.##} in foreign currency. "
                        + "There is no column for it on RetailLegacyPurchase, so only the local-currency cost is stored.");
                }

                _snapshot.PurchasesByKey.TryGetValue(RetailNaming.Key(row.Key), out var existing);
                var supplierKey = row.Supplier is null ? null : RetailNaming.Key(RetailNaming.SplitSupplierAndCountry(row.Supplier).Name);

                var changes = existing is null
                    ? Array.Empty<RetailFieldChange>()
                    : Differences(row, existing, supplierKey, supplierByKey);

                planned.Add(new PlannedPurchase(
                    row,
                    existing is null
                        ? RetailSyncAction.Create
                        : changes.Count > 0 ? RetailSyncAction.Update : RetailSyncAction.Unchanged,
                    existing,
                    supplierKey,
                    changes,
                    null));
            }

            foreach (var purchase in planned)
            {
                _report.LegacyPurchases.Add(new RetailPurchasePlan(
                    purchase.Row.Row, purchase.Row.Key, purchase.Row.Number, purchase.Action,
                    purchase.Existing?.Id, purchase.Changes, purchase.Reason));
            }

            return planned;
        }

        private static IReadOnlyList<RetailFieldChange> Differences(
            WorkbookPurchaseRow row,
            SnapshotPurchase existing,
            string? supplierKey,
            IReadOnlyDictionary<string, PlannedSupplier> suppliers)
        {
            var changes = new List<RetailFieldChange>();

            Compare("date", existing.OrderDateUtc.Date, row.Date!.Value.Date);
            Compare("invoice", existing.InvoiceNumber, row.InvoiceNumber);
            Compare("quantity", existing.QuantityPieces, RetailRounding.Money(row.Quantity));
            Compare("supplier cost", existing.SupplierCost, RetailRounding.Money(row.SupplierCost));
            Compare("shipping", existing.ShippingCustomsClearance, RetailRounding.Money(row.ShippingCustomsClearance));
            Compare("status", existing.Status, row.Status);
            Compare("supplier name", existing.SupplierNameSnapshot, row.Supplier ?? string.Empty);
            Compare("workbook row", existing.SourceRowNumber, row.Row);

            var supplierId = supplierKey is null ? null : suppliers.GetValueOrDefault(supplierKey)?.ExistingId;
            if (row.Supplier is not null && supplierId != existing.SupplierId)
                changes.Add(new RetailFieldChange("supplier", existing.SupplierId?.ToString(), suppliers.GetValueOrDefault(supplierKey!)?.Name));

            return changes;

            void Compare<T>(string field, T from, T to)
            {
                if (!EqualityComparer<T>.Default.Equals(from, to))
                    changes.Add(new RetailFieldChange(field, from?.ToString(), to?.ToString()));
            }
        }

        // ── Stock ────────────────────────────────────────────────────────────

        /// <summary>
        /// Reconciles the workbook's contribution to the ledger instead of replaying it. The
        /// target is the workbook's received quantity; what is already there is the sum of the
        /// movements the workbook itself produced, identified by
        /// <see cref="RetailStockSourceDocument.WorkbookImport"/>. Only the difference is
        /// posted, so a changed quantity corrects rather than doubles, and a second run
        /// computes a difference of zero and writes nothing.
        ///
        /// Purchase receipts, manual adjustments and sales are excluded from the target: they
        /// are operational facts the workbook has no authority over.
        /// </summary>
        private IReadOnlyList<PlannedStock> PlanStock(IReadOnlyList<PlannedProduct> products)
        {
            var planned = new List<PlannedStock>();

            foreach (var product in products)
            {
                if (product.Action is RetailSyncAction.Skip or RetailSyncAction.Conflict)
                    continue;

                var target = RetailRounding.Money(product.Row.ReceivedQuantity);
                var unitCost = RetailRounding.Money(product.Row.CostPerItem);

                if (product.Existing is not { HasOpeningMovement: true } existing)
                {
                    if (target > 0m)
                        planned.Add(new PlannedStock(product.Row.SkuKey, product.Row.Sku,
                            RetailStockMovementType.OpeningBalance, target, unitCost,
                            "Imported opening position (workbook AVAILABLE SOH)"));
                    continue;
                }

                var delta = target - existing.WorkbookOriginInflow;
                if (delta == 0m)
                    continue;

                var type = delta > 0m ? RetailStockMovementType.AdjustmentIncrease : RetailStockMovementType.AdjustmentDecrease;
                planned.Add(new PlannedStock(product.Row.SkuKey, product.Row.Sku, type, Math.Abs(delta), unitCost,
                    $"Workbook received quantity changed from {existing.WorkbookOriginInflow:0.##} to {target:0.##}"));
            }

            foreach (var posting in planned)
                _report.StockPostings.Add(new RetailStockPlan(posting.Sku, posting.MovementType, posting.Quantity, posting.UnitCost, posting.Reason));

            return planned;
        }

        // ── Historical payments ──────────────────────────────────────────────

        /// <summary>
        /// Applies the confirmed settlement ruling to every order the importer has ever written
        /// to this branch, including ones a workbook row no longer matches.
        ///
        /// Three corrections, all metadata: a settled sale with no Payment row is settled in
        /// cash; a Payment row with no usable method is restated as cash; a Payment stamped
        /// with the date the importer ran is moved to the date the sale happened.
        ///
        /// Deliberately does NOT touch: an order that is explicitly unpaid (a spreadsheet does
        /// not collect money), a gift (completed, nothing collected), a zero-value line, or a
        /// payment that already names a method the workbook gave explicitly.
        /// </summary>
        private IReadOnlyList<PlannedPaymentFix> PlanPaymentFixes(IReadOnlyList<PlannedSale> sales)
        {
            // Sales the workbook is settling in this same run are the sale plan's job; counting
            // them here as well would report the work twice and attempt it twice.
            var settledByPlan = sales
                .Where(s => s.Settlement.IsPaid)
                .Select(s => s.Existing?.OrderId)
                .OfType<Guid>()
                .ToHashSet();

            var fixes = new List<PlannedPaymentFix>();

            foreach (var order in _snapshot.HistoricalOrders.OrderBy(o => o.OrderNumber, StringComparer.Ordinal))
            {
                foreach (var payment in order.Payments)
                {
                    if (IsUnreliable(payment))
                        fixes.Add(Fix(order, payment.Id, RetailPaymentFixKind.UnreliableMethod, payment.Amount,
                            $"method '{payment.Method ?? "(none)"}' is not usable — restated as {RetailPaymentMapping.CashName}"));

                    if (payment.CreatedAtUtc.Date != order.CreatedAtUtc.Date)
                        fixes.Add(Fix(order, payment.Id, RetailPaymentFixKind.TransactionDate, payment.Amount,
                            $"recorded {payment.CreatedAtUtc:yyyy-MM-dd}, sold {order.CreatedAtUtc:yyyy-MM-dd}"));
                }

                if (!NeedsSettlement(order) || settledByPlan.Contains(order.OrderId))
                    continue;

                fixes.Add(Fix(order, null, RetailPaymentFixKind.MissingSettlement, order.TotalAmount,
                    $"settled sale of {order.TotalAmount:0.00} with no payment recorded — settled in {RetailPaymentMapping.CashName}"));

                _report.AddReview(RetailSheets.Sellout, 0,
                    $"{order.OrderNumber} is settled but its workbook row is gone; the confirmed ruling settles it in cash.");
            }

            foreach (var fix in fixes)
                _report.PaymentFixes.Add(new RetailPaymentFix(fix.OrderNumber, fix.Kind, fix.Amount, fix.Detail));

            return fixes;

            PlannedPaymentFix Fix(SnapshotHistoricalOrder order, Guid? paymentId, RetailPaymentFixKind kind, decimal amount, string detail)
                => new(order.OrderId, order.OrderNumber, paymentId, kind, amount, order.CreatedAtUtc, detail);
        }

        /// <summary>A payment carries no method the till or a report can act on.</summary>
        private static bool IsUnreliable(SnapshotPayment payment)
            => payment.PaymentMethodId is null || string.IsNullOrWhiteSpace(payment.Method);

        /// <summary>
        /// The order is settled and the money is missing. Unpaid orders are never converted:
        /// only a terminal paid status counts, the amount must be real, and a gift is completed
        /// precisely because nothing was collected.
        /// </summary>
        private static bool NeedsSettlement(SnapshotHistoricalOrder order)
            => order.Status is OrderStatus.Completed or OrderStatus.Paid
               && order.TotalAmount > 0m
               && order.Payments.Count == 0
               && !RetailPaymentMapping.IsGift(order.PaymentMethod);

        /// <summary>The settlement position the sync will leave behind, classified so the
        /// classes are disjoint and sum back to every imported order.</summary>
        private void SummarisePayments(IReadOnlyList<PlannedSale> sales)
        {
            var planned = sales
                .Where(s => s.Existing is not null)
                .ToDictionary(s => s.Existing!.OrderId, s => s.Settlement);

            var rows = new List<(string Class, int Orders, decimal Value, int Rows, decimal Paid)>();

            foreach (var group in _snapshot.HistoricalOrders.GroupBy(Classify))
            {
                rows.Add((
                    group.Key,
                    group.Count(),
                    group.Sum(o => o.TotalAmount),
                    group.Sum(o => Math.Max(o.Payments.Count, WillSettle(o) ? 1 : 0)),
                    group.Sum(o => o.Payments.Count > 0 ? o.Payments.Sum(p => p.Amount) : WillSettle(o) ? o.TotalAmount : 0m)));
            }

            foreach (var row in rows.OrderBy(r => r.Class, StringComparer.Ordinal))
                _report.PaymentSummary.Add(new RetailPaymentSummaryRow(row.Class, row.Orders, row.Value, row.Rows, row.Paid));

            // Sales the workbook adds in this run are not in the snapshot; report them apart so
            // the classes above stay a faithful before-picture of what was already stored.
            var created = sales.Where(s => s.Action == RetailSyncAction.Create).ToList();
            if (created.Count > 0)
            {
                _report.PaymentSummary.Add(new RetailPaymentSummaryRow(
                    "Z. new sales added by this run",
                    created.Count,
                    created.Sum(s => s.Row.LineTotal),
                    created.Count(s => s.Settlement.IsPaid && s.Row.LineTotal > 0m),
                    created.Where(s => s.Settlement.IsPaid).Sum(s => s.Row.LineTotal)));
            }

            string Classify(SnapshotHistoricalOrder order)
            {
                if (order.Status is OrderStatus.Served or OrderStatus.New or OrderStatus.Preparing or OrderStatus.Ready)
                    return "A. explicitly NOT PAID";
                if (order.TotalAmount <= 0m || RetailPaymentMapping.IsGift(order.PaymentMethod))
                    return "B. gift / zero value, nothing to settle";
                if (order.Payments.Count == 0)
                    return WillSettle(order) ? "C. settled in cash by this run" : "F. settled, still without a payment";
                return IsUnreliable(order.Payments[0])
                    ? "E. payment restated as cash by this run"
                    : $"D. explicit {order.Payments[0].Method}";
            }

            bool WillSettle(SnapshotHistoricalOrder order)
                => order.Payments.Count == 0
                   && order.TotalAmount > 0m
                   && (planned.TryGetValue(order.OrderId, out var settlement) ? settlement.IsPaid : NeedsSettlement(order));
        }

        // ── Verification ─────────────────────────────────────────────────────

        private void Publish(
            IReadOnlyList<PlannedProduct> products,
            IReadOnlyList<PlannedSale> sales,
            IReadOnlyList<PlannedStock> stock)
        {
            _report.PreSyncOnHand = _snapshot.OnHand;
            _report.PreSyncMovements = _snapshot.MovementCount;

            var posted = stock.Sum(s => RetailStockMovement.SignedQuantity(s.MovementType, s.Quantity));
            var sold = sales.Where(s => s.Action == RetailSyncAction.Create).Sum(s => (decimal)s.Row.Quantity);
            _report.ExpectedOnHand = _snapshot.OnHand + posted - sold;

            _report.BranchAssignments.Created = products.Count(p =>
                p.Action is not (RetailSyncAction.Skip or RetailSyncAction.Conflict)
                && p.Existing?.AssignedToRetailBranch != true);
            _report.BranchAssignments.Unchanged = products.Count(p => p.Existing?.AssignedToRetailBranch == true);
        }

        private void VerifyInventory(
            IReadOnlyList<PlannedProduct> products,
            IReadOnlyList<PlannedSale> sales,
            IReadOnlyList<PlannedStock> stock)
        {
            var inventory = _reader.ReadLiveInventory().ToDictionary(r => r.SkuKey, r => r);
            if (inventory.Count == 0)
                return;

            var postings = stock.ToDictionary(s => s.SkuKey,
                s => RetailStockMovement.SignedQuantity(s.MovementType, s.Quantity));

            var newSales = sales
                .Where(s => s.Action == RetailSyncAction.Create)
                .GroupBy(s => s.Row.SkuKey)
                .ToDictionary(g => g.Key, g => (decimal)g.Sum(s => s.Row.Quantity));

            var orphans = _report.OrphanSales
                .GroupBy(o => o.Sku)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var product in products.Where(p => p.Action is not (RetailSyncAction.Skip or RetailSyncAction.Conflict)))
            {
                if (!inventory.TryGetValue(product.Row.SkuKey, out var workbook))
                {
                    _report.AddReview(RetailSheets.LiveInventory, product.Row.Row,
                        $"SKU '{product.Row.Sku}' is in PRODUCT MASTER but has no LIVE INVENTORY row — its stock cannot be verified.");
                    continue;
                }

                var expected = (product.Existing?.OnHand ?? 0m)
                               + postings.GetValueOrDefault(product.Row.SkuKey)
                               - newSales.GetValueOrDefault(product.Row.SkuKey);

                var explanation = orphans.TryGetValue(product.Row.SkuKey, out var rows)
                    ? $"{rows.Count} sale(s) recorded in the database ({string.Join(", ", rows.Select(r => r.OrderNumber))}) no longer exist in the workbook"
                    : null;

                _report.InventoryChecks.Add(new RetailInventoryCheck(product.Row.Sku, expected, workbook.OnHand, explanation));

                if (expected < 0m)
                {
                    _report.AddReview(RetailSheets.ProductMaster, product.Row.Row,
                        $"SKU '{product.Row.Sku}' would end the sync at {expected:0.##} units — the workbook records more sold than received.");
                }
            }
        }

        private void VerifyProfit(IReadOnlyList<PlannedSale> sales, IReadOnlyList<PlannedProduct> products)
        {
            var profit = _reader.ReadSummaryProfit();
            if (profit.Count == 0)
                return;

            var cost = products.ToDictionary(p => p.Row.SkuKey, p => RetailRounding.Money(p.Row.CostPerItem) ?? 0m);
            var created = sales.Where(s => s.Action == RetailSyncAction.Create).ToList();

            var systemUnits = _snapshot.RecordedUnits + created.Sum(s => s.Row.Quantity);
            var systemSales = _snapshot.RecordedSales + created.Sum(s => s.Row.LineTotal);
            var systemCost = _snapshot.RecordedCogs + created.Sum(s => cost.GetValueOrDefault(s.Row.SkuKey) * s.Row.Quantity);

            _report.Profit = new RetailProfitCheck(
                profit.Sum(p => p.Quantity),
                profit.Sum(p => p.Sales),
                profit.Sum(p => p.TotalCost),
                profit.Sum(p => p.Profit),
                systemUnits,
                systemSales,
                decimal.Round(systemCost, 2, MidpointRounding.AwayFromZero),
                decimal.Round(systemSales - systemCost, 2, MidpointRounding.AwayFromZero));
        }
    }

    /// <summary>Rounds workbook figures to the scale their column actually stores, so a plan
    /// compares like with like and a second run finds nothing to change.</summary>
    internal static class RetailRounding
    {
        public static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        public static decimal? Money(decimal? value) => value.HasValue ? Money(value.Value) : null;
        public static decimal Shipping(decimal value) => decimal.Round(value, 3, MidpointRounding.AwayFromZero);
        public static decimal? Margin(decimal? fraction) => fraction.HasValue ? decimal.Round(fraction.Value * 100m, 2, MidpointRounding.AwayFromZero) : null;
    }
}
