using ClosedXML.Excel;

namespace RestaurantPos.Api.Modules.Retail.Import
{
    internal sealed record WorkbookProductRow(
        int Row,
        string Sku,
        string SkuKey,
        string? Barcode,
        string? Brand,
        string? Name,
        string? Category,
        string? Size,
        string? Supplier,
        decimal ReceivedQuantity,
        decimal SupplierCostTotal,
        decimal ShippingCostPerUnit,
        decimal? CostPerItem,
        decimal? TargetMarginFraction,
        decimal ApprovedSellingPrice);

    internal sealed record WorkbookSaleRow(
        int Row,
        DateTime? Date,
        string Sku,
        string SkuKey,
        int Quantity,
        decimal SoldUnitPrice,
        decimal? ApprovedUnitPrice,
        string? CustomerName,
        string? CustomerPhone,
        string? Payment)
    {
        public decimal LineTotal => decimal.Round(SoldUnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);
    }

    internal sealed record WorkbookPurchaseRow(
        int Row,
        string Number,
        string Key,
        DateTime? Date,
        string? Supplier,
        string? InvoiceNumber,
        decimal Quantity,
        decimal SupplierCost,
        decimal ShippingCustomsClearance,
        decimal? ForeignCurrencyCost,
        string? Status);

    internal sealed record WorkbookInventoryRow(string SkuKey, decimal Received, decimal Sold, decimal OnHand);

    internal sealed record WorkbookProfitRow(string SkuKey, decimal Quantity, decimal Sales, decimal TotalCost, decimal Profit);

    internal sealed record WorkbookSupplierRow(
        int Row,
        string Name,
        string? Brands,
        string? BestSellers,
        string? Contact,
        string? Country,
        string? Website,
        string? WholesaleContact);

    /// <summary>
    /// Turns the workbook into typed rows and nothing else — no database, no decisions. Every
    /// sheet is read through <see cref="SheetColumns"/>, so a revision that inserts a column
    /// shifts nothing.
    /// </summary>
    internal sealed class RetailWorkbookReader : IDisposable
    {
        private readonly XLWorkbook _workbook;

        public RetailWorkbookReader(string workbookPath)
        {
            _workbook = new XLWorkbook(workbookPath);
        }

        public IReadOnlyList<string> SheetNames => _workbook.Worksheets.Select(w => w.Name).ToList();

        public bool HasSheet(string name) => _workbook.TryGetWorksheet(name, out _);

        public IReadOnlyList<WorkbookProductRow> ReadProducts()
        {
            if (!_workbook.TryGetWorksheet(RetailSheets.ProductMaster, out var sheet))
                return Array.Empty<WorkbookProductRow>();

            var columns = SheetColumns.From(sheet);
            var sku = columns.Require(ProductHeaders.Sku);
            var rows = new List<WorkbookProductRow>();

            foreach (var row in DataRows(sheet, sku))
            {
                var value = RetailNaming.Sku(WorkbookCell.Text(sheet, row, sku))!;
                rows.Add(new WorkbookProductRow(
                    Row: row,
                    Sku: value,
                    SkuKey: RetailNaming.SkuKey(value),
                    Barcode: Text(sheet, columns, row, ProductHeaders.Barcode),
                    Brand: Text(sheet, columns, row, ProductHeaders.Brand),
                    Name: Text(sheet, columns, row, ProductHeaders.Name),
                    Category: Text(sheet, columns, row, ProductHeaders.Category),
                    Size: Text(sheet, columns, row, ProductHeaders.Size),
                    Supplier: Text(sheet, columns, row, ProductHeaders.Supplier),
                    ReceivedQuantity: Number(sheet, columns, row, ProductHeaders.ReceivedQuantity) ?? 0m,
                    SupplierCostTotal: Number(sheet, columns, row, ProductHeaders.SupplierCostTotal) ?? 0m,
                    ShippingCostPerUnit: Number(sheet, columns, row, ProductHeaders.ShippingCostPerUnit) ?? 0m,
                    CostPerItem: Number(sheet, columns, row, ProductHeaders.CostPerItem),
                    TargetMarginFraction: Number(sheet, columns, row, ProductHeaders.TargetMargin),
                    ApprovedSellingPrice: Number(sheet, columns, row, ProductHeaders.ApprovedSellingPrice) ?? 0m));
            }

            return rows;
        }

        public IReadOnlyList<WorkbookSaleRow> ReadSales()
        {
            if (!_workbook.TryGetWorksheet(RetailSheets.Sellout, out var sheet))
                return Array.Empty<WorkbookSaleRow>();

            var columns = SheetColumns.From(sheet);
            var sku = columns.Require(SalesHeaders.Sku);
            var rows = new List<WorkbookSaleRow>();

            foreach (var row in DataRows(sheet, sku))
            {
                var value = RetailNaming.Sku(WorkbookCell.Text(sheet, row, sku))!;
                rows.Add(new WorkbookSaleRow(
                    Row: row,
                    Date: Date(sheet, columns, row, SalesHeaders.Date),
                    Sku: value,
                    SkuKey: RetailNaming.SkuKey(value),
                    Quantity: (int)(Number(sheet, columns, row, SalesHeaders.Quantity) ?? 0m),
                    SoldUnitPrice: Number(sheet, columns, row, SalesHeaders.SoldUnitPrice) ?? 0m,
                    ApprovedUnitPrice: Number(sheet, columns, row, SalesHeaders.ApprovedUnitPrice),
                    CustomerName: Text(sheet, columns, row, SalesHeaders.CustomerName),
                    CustomerPhone: Text(sheet, columns, row, SalesHeaders.CustomerPhone),
                    Payment: Text(sheet, columns, row, SalesHeaders.Payment)));
            }

            return rows;
        }

        /// <summary>
        /// Reads the purchase headers and assigns each one its historical key. PO NUMBER is
        /// the identity while it is unique on the sheet; the 2026-09 revision reuses
        /// "THAILAND" for six unrelated documents, so those fall back to PO NUMBER + invoice
        /// rather than collapsing into one record.
        /// </summary>
        public IReadOnlyList<WorkbookPurchaseRow> ReadPurchases()
        {
            if (!_workbook.TryGetWorksheet(RetailSheets.PurchaseOrders, out var sheet))
                return Array.Empty<WorkbookPurchaseRow>();

            var columns = SheetColumns.From(sheet);
            var number = columns.Require(PurchaseOrderHeaders.Number);
            var raw = DataRows(sheet, number)
                .Select(row => (Row: row, Number: WorkbookCell.Text(sheet, row, number)!.Trim()))
                .ToList();

            var repeated = raw
                .GroupBy(r => RetailNaming.Key(r.Number))
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet();

            return raw.Select(r =>
            {
                var invoice = Text(sheet, columns, r.Row, PurchaseOrderHeaders.InvoiceNumber);
                var key = repeated.Contains(RetailNaming.Key(r.Number)) && invoice is not null
                    ? $"{r.Number} / {invoice}"
                    : r.Number;

                return new WorkbookPurchaseRow(
                    Row: r.Row,
                    Number: r.Number,
                    Key: RetailNaming.Truncate(key, 50),
                    Date: Date(sheet, columns, r.Row, PurchaseOrderHeaders.Date),
                    Supplier: Text(sheet, columns, r.Row, PurchaseOrderHeaders.Supplier),
                    InvoiceNumber: invoice,
                    Quantity: Number(sheet, columns, r.Row, PurchaseOrderHeaders.Quantity) ?? 0m,
                    SupplierCost: Number(sheet, columns, r.Row, PurchaseOrderHeaders.SupplierCost) ?? 0m,
                    ShippingCustomsClearance: Number(sheet, columns, r.Row, PurchaseOrderHeaders.ShippingCustomsClearance) ?? 0m,
                    ForeignCurrencyCost: Number(sheet, columns, r.Row, PurchaseOrderHeaders.ForeignCurrencyCost),
                    Status: Text(sheet, columns, r.Row, PurchaseOrderHeaders.Status));
            }).ToList();
        }

        public IReadOnlyList<WorkbookInventoryRow> ReadLiveInventory()
        {
            if (!_workbook.TryGetWorksheet(RetailSheets.LiveInventory, out var sheet))
                return Array.Empty<WorkbookInventoryRow>();

            var columns = SheetColumns.From(sheet);
            var sku = columns.Require(LiveInventoryHeaders.Sku);

            return DataRows(sheet, sku)
                .Select(row => new WorkbookInventoryRow(
                    RetailNaming.SkuKey(WorkbookCell.Text(sheet, row, sku)),
                    Number(sheet, columns, row, LiveInventoryHeaders.Received) ?? 0m,
                    Number(sheet, columns, row, LiveInventoryHeaders.Sold) ?? 0m,
                    Number(sheet, columns, row, LiveInventoryHeaders.OnHand) ?? 0m))
                .ToList();
        }

        public IReadOnlyList<WorkbookProfitRow> ReadSummaryProfit()
        {
            if (!_workbook.TryGetWorksheet(RetailSheets.SummaryProfit, out var sheet))
                return Array.Empty<WorkbookProfitRow>();

            var columns = SheetColumns.From(sheet);
            var sku = columns.Require(SummaryProfitHeaders.Sku);

            return DataRows(sheet, sku)
                .Select(row => new WorkbookProfitRow(
                    RetailNaming.SkuKey(WorkbookCell.Text(sheet, row, sku)),
                    Number(sheet, columns, row, SummaryProfitHeaders.Quantity) ?? 0m,
                    Number(sheet, columns, row, SummaryProfitHeaders.Sales) ?? 0m,
                    Number(sheet, columns, row, SummaryProfitHeaders.TotalCost) ?? 0m,
                    Number(sheet, columns, row, SummaryProfitHeaders.Profit) ?? 0m))
                .ToList();
        }

        public IReadOnlyList<WorkbookSupplierRow> ReadSuppliers()
        {
            if (!_workbook.TryGetWorksheet(RetailSheets.Suppliers, out var sheet))
                return Array.Empty<WorkbookSupplierRow>();

            var columns = SheetColumns.From(sheet);
            var name = columns.Require(SupplierHeaders.Name);

            return DataRows(sheet, name)
                .Select(row => new WorkbookSupplierRow(
                    row,
                    WorkbookCell.Text(sheet, row, name)!,
                    Text(sheet, columns, row, SupplierHeaders.Brand),
                    Text(sheet, columns, row, SupplierHeaders.BestSellers),
                    Text(sheet, columns, row, SupplierHeaders.Contact),
                    Text(sheet, columns, row, SupplierHeaders.Country),
                    Text(sheet, columns, row, SupplierHeaders.Website),
                    Text(sheet, columns, row, SupplierHeaders.WholesaleContact)))
                .ToList();
        }

        /// <summary>Headers present on a sheet that the importer deliberately does not read.</summary>
        public IReadOnlyList<string> UnimportedHeaders(string sheetName, IReadOnlyList<string> candidates)
        {
            if (!_workbook.TryGetWorksheet(sheetName, out var sheet))
                return Array.Empty<string>();

            var columns = SheetColumns.From(sheet);
            return candidates.Where(c => columns.Find(c) is not null).ToList();
        }

        private static IEnumerable<int> DataRows(IXLWorksheet sheet, int keyColumn)
        {
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var row = 2; row <= lastRow; row++)
            {
                if (WorkbookCell.Text(sheet, row, keyColumn) is not null)
                    yield return row;
            }
        }

        private static string? Text(IXLWorksheet sheet, SheetColumns columns, int row, string header)
            => columns.Find(header) is { } column ? WorkbookCell.Text(sheet, row, column) : null;

        private static decimal? Number(IXLWorksheet sheet, SheetColumns columns, int row, string header)
            => columns.Find(header) is { } column ? WorkbookCell.Number(sheet, row, column) : null;

        private static DateTime? Date(IXLWorksheet sheet, SheetColumns columns, int row, string header)
            => columns.Find(header) is { } column ? WorkbookCell.Date(sheet, row, column) : null;

        public void Dispose() => _workbook.Dispose();
    }
}
