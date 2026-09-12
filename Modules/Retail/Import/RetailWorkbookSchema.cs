using System.Text.RegularExpressions;
using ClosedXML.Excel;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.Retail.Import
{
    internal static class RetailSheets
    {
        public const string ProductMaster = "PRODUCT MASTER";
        public const string Sellout = "SELLOUT 2026";
        public const string Suppliers = "SUPPLIER INFORMATIONS";
        public const string PurchaseOrders = "PURCHASE ORDER";
        public const string LiveInventory = "LIVE INVENTORY";
        public const string SummaryProfit = "SUMMARY PROFIT";
    }

    /// <summary>
    /// Column positions resolved from the header row rather than hard-coded.
    ///
    /// The workbook is hand-maintained and gains columns between revisions — the 2026-09
    /// revision inserted PO SUPPLIER FOREIGN CURRENCY COST in the middle of PURCHASE ORDER,
    /// which silently shifted every column after it. Reading by position would have imported
    /// the foreign-currency figure as the supplier cost and the supplier cost as shipping.
    /// </summary>
    internal sealed class SheetColumns
    {
        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

        private readonly IReadOnlyList<(string Header, int Column)> _headers;
        private readonly string _sheetName;

        private SheetColumns(string sheetName, IReadOnlyList<(string, int)> headers)
        {
            _sheetName = sheetName;
            _headers = headers;
        }

        public static SheetColumns From(IXLWorksheet sheet)
        {
            var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            var headers = new List<(string, int)>(lastColumn);

            for (var column = 1; column <= lastColumn; column++)
            {
                var header = Normalise(WorkbookCell.Text(sheet, 1, column));
                if (header.Length > 0)
                    headers.Add((header, column));
            }

            return new SheetColumns(sheet.Name, headers);
        }

        /// <summary>Resolves a column, preferring an exact header match and falling back to a
        /// unique prefix match so long descriptive headers keep working.</summary>
        public int? Find(string header)
        {
            var wanted = Normalise(header);

            var exact = _headers.Where(h => h.Header == wanted).ToList();
            if (exact.Count == 1)
                return exact[0].Column;

            var prefixed = _headers.Where(h => h.Header.StartsWith(wanted, StringComparison.Ordinal)).ToList();
            return prefixed.Count == 1 ? prefixed[0].Column : null;
        }

        public int Require(string header)
            => Find(header) ?? throw new InvalidOperationException(
                $"Sheet '{_sheetName}' has no column matching '{header}'. Headers found: {string.Join(" | ", _headers.Select(h => h.Header))}");

        private static string Normalise(string? value)
            => Whitespace.Replace((value ?? string.Empty).Trim(), " ").ToUpperInvariant();
    }

    /// <summary>Header texts the importer resolves. Values are matched exactly first, then as
    /// a unique prefix — so "SIZE" finds "SIZE ML/G" but "PO SUPPLIER COST" never resolves to
    /// "PO SUPPLIER FOREIGN CURRENCY COST".</summary>
    internal static class ProductHeaders
    {
        public const string Sku = "SKU";
        public const string Barcode = "BARCODE";
        public const string Brand = "BRAND";
        public const string Name = "PRODUCT NAME";
        public const string Category = "CATEGORY";
        public const string Size = "SIZE";
        public const string Supplier = "SUPPLIER NAME";
        public const string ReceivedQuantity = "AVAILABLE SOH";
        public const string SupplierCostTotal = "TOTAL SUPPLIER COST";
        public const string ShippingCostPerUnit = "SHIPPING COST PER UNIT";
        public const string CostPerItem = "COST PER ITEM";
        public const string TargetMargin = "TARGET MARGIN";
        public const string ApprovedSellingPrice = "APPROVED SELLING PRICE";

        /// <summary>Calculated by the sheet and re-derived by the application, so never read:
        /// TOTAL COST, SELLING PRICE, PROFIT PER UNIT. THAILAND PRICE PER PIECE / TOTAL
        /// THAILAND PRICE are supplier-currency figures with no column to land in.</summary>
        public static readonly string[] NotImported =
        {
            "TOTAL COST", "SELLING PRICE", "PROFIT PER UNIT",
            "THAILAND PRICE PER PIECE", "TOTAL THAILAND PRICE"
        };
    }

    internal static class SalesHeaders
    {
        public const string Date = "DATE";
        public const string CustomerName = "NAMES";
        public const string CustomerPhone = "PHONE NB";
        public const string Sku = "SKU";
        public const string Quantity = "QTY";
        public const string ApprovedUnitPrice = "APPROVED SELLING PRICE";
        public const string SoldUnitPrice = "SOLD PRICE/UNIT";
        public const string Payment = "PAYMENT";

        /// <summary>VLOOKUP results the importer re-derives: PRODUCT NAME, SUPPLIER NAME,
        /// TOTAL AMOUNT.</summary>
        public static readonly string[] NotImported = { "PRODUCT NAME", "SUPPLIER NAME", "TOTAL AMOUNT" };
    }

    internal static class PurchaseOrderHeaders
    {
        public const string Date = "DATE";
        public const string Number = "PO NUMBER";
        public const string Supplier = "SUPPLIER";
        public const string InvoiceNumber = "INV.#";
        public const string Quantity = "QTY OF ORDER";
        public const string SupplierCost = "PO SUPPLIER COST";
        public const string ForeignCurrencyCost = "PO SUPPLIER FOREIGN CURRENCY COST";
        public const string ShippingCustomsClearance = "SHIPPING-CUSTOM-CLEARANCE";
        public const string Status = "STATUS";

        /// <summary>TOTAL COST OF PO is the sheet's own F + G and is recomputed. The foreign
        /// currency column has no home in <c>RetailLegacyPurchase</c> and is reported instead
        /// of being dropped silently.</summary>
        public static readonly string[] NotImported = { "TOTAL COST OF PO" };
    }

    internal static class LiveInventoryHeaders
    {
        public const string Sku = "SKU";
        public const string Received = "TOTAL RECEIVED QTY";
        public const string Sold = "TOTAL SOLD QTY";
        public const string OnHand = "CURRENT SOH";
    }

    internal static class SummaryProfitHeaders
    {
        public const string Sku = "SKU";
        public const string Quantity = "QTY SOLD";
        public const string Sales = "TOTAL SALES";
        public const string TotalCost = "TOTAL COST";
        public const string Profit = "PROFIT";
    }

    internal static class SupplierHeaders
    {
        public const string Name = "SUPPLIER NAME COMPANY";
        public const string Brand = "BRAND";
        public const string BestSellers = "BEST SELLER PRODUCT";
        public const string Contact = "CONTACT NUMBER";
        public const string Country = "COUNTRY";
        public const string Website = "WEBSITE";
        public const string WholesaleContact = "EMAIL ADRESS FOR WHOLESALE";
    }

    internal static class RetailNaming
    {
        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex PhoneDigits = new(@"[^\d+]", RegexOptions.Compiled);
        private static readonly Regex ValidPhone = new(@"^\+?[0-9]{7,15}$", RegexOptions.Compiled);
        private static readonly string[] Placeholders = { "N/A", "NA", "-", "—", "NONE" };

        /// <summary>Case- and whitespace-insensitive matching key for a natural-key name.</summary>
        public static string Key(string? name)
            => Whitespace.Replace((name ?? string.Empty).Trim(), " ").ToUpperInvariant();

        /// <summary>
        /// The value stored as the SKU. <see cref="WorkbookCell.Text"/> has already turned a
        /// numeric cell into its plain integer form, so 6290362346531 and "6290362346531"
        /// arrive identical; this only removes the padding the sheet accumulates.
        /// </summary>
        public static string? Sku(string? raw)
        {
            var value = Whitespace.Replace((raw ?? string.Empty).Trim(), " ");
            return value.Length == 0 ? null : value;
        }

        /// <summary>Matching key for a SKU. Case-insensitive so "rll rosc" cannot create a
        /// second product beside "RLL ROSC"; internal spacing is collapsed but never removed,
        /// because "SKINTIFIC" and "SKINTIFIC 1" are different products.</summary>
        public static string SkuKey(string? raw) => Key(Sku(raw));

        public static string? MeaningfulOrNull(string? value)
            => value is null || Placeholders.Contains(value.Trim().ToUpperInvariant()) ? null : value.Trim();

        public static bool LooksLikeEmail(string? value)
            => value is not null && value.Contains('@') && !value.StartsWith("http", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The workbook's PHONE NB. column repeats the customer's name in every populated
        /// row — it holds no phone numbers at all. Storing a name in a phone field would be
        /// a lie the rest of the system then acts on (search, receipts, customer matching),
        /// so anything without enough digits is dropped and only the name is kept.
        /// </summary>
        public static string? PhoneOrNull(string? value)
        {
            if (MeaningfulOrNull(value) is not { } raw)
                return null;

            var digits = raw.Count(char.IsDigit);
            return digits >= 6 ? raw : null;
        }

        /// <summary>Strips formatting a phone column has accumulated. Returns null when the
        /// result still fails the Supplier phone contract rather than storing something
        /// the existing procurement screens would reject on the next edit.</summary>
        public static string? NormalisePhone(string? value)
        {
            if (MeaningfulOrNull(value) is not { } raw)
                return null;

            var digits = PhoneDigits.Replace(raw, string.Empty);
            if (digits.LastIndexOf('+') > 0)
                digits = "+" + digits.Replace("+", string.Empty);

            return ValidPhone.IsMatch(digits) ? digits : null;
        }

        /// <summary>"SILVER STAR TRADING-DOHA" → ("SILVER STAR TRADING", "DOHA"). The product
        /// sheet's supplier column is documented as name + country of origin.</summary>
        public static (string Name, string? Country) SplitSupplierAndCountry(string raw)
        {
            var value = Whitespace.Replace(raw.Trim(), " ");
            var separator = value.LastIndexOf('-');
            if (separator <= 0 || separator == value.Length - 1)
                return (value, null);

            var country = value[(separator + 1)..].Trim();
            if (country.Length is 0 or > 20 || country.Any(char.IsDigit))
                return (value, null);

            return (value[..separator].Trim(), country);
        }

        public static string Truncate(string? value, int max)
            => value is null ? string.Empty : value.Length <= max ? value : value[..max];
    }

    /// <summary>How a workbook PAYMENT value settles: it mixes payment method
    /// ("CASH", "TRANSFER"), payment status ("NOT PAID", "PAID") and transaction type
    /// ("GIFT").</summary>
    internal sealed record RetailSettlement(
        OrderStatus Status,
        bool IsPaid,
        string? MethodCode,
        string? MethodName,
        bool IsUnknown,
        string? RawValue);

    internal static class RetailPaymentMapping
    {
        public const string CashCode = "CASH";
        public const string CashName = "Cash";
        private const string TransferCode = "BANK_TRANSFER";
        private const string GiftMarker = "GIFT";

        /// <summary>Payment methods the workbook settles in. Both must exist and be enabled on
        /// the retail branch before the till can take money.</summary>
        public static readonly string[] RequiredCodes = { CashCode, TransferCode };

        /// <summary>
        /// Confirmed business ruling (2026-09-06) for DOHA LUXE historical workbook sales:
        /// <c>PAID = CASH</c>, <c>CASH = CASH</c>, <c>NOT PAID = unpaid</c>. A workbook sale
        /// that is known to be settled but names no reliable method settles in cash — the shop
        /// takes cash at the counter, so an unstated method is not an unknown one.
        ///
        /// The ruling does NOT reach the two cases that are not settlements at all: a
        /// zero-value line has no money to record, and an unrecognised value is not evidence
        /// that anything was collected, so neither is turned into a payment.
        /// </summary>
        public static RetailSettlement Resolve(string? rawPayment, decimal lineTotal)
        {
            var value = (rawPayment ?? string.Empty).Trim().ToUpperInvariant();

            // A zero-value line cannot carry a payment, whatever the column says.
            if (lineTotal <= 0m)
                return new RetailSettlement(OrderStatus.Completed, false, null, value.Length == 0 ? null : rawPayment, false, rawPayment);

            if (IsGift(value))
                return new RetailSettlement(OrderStatus.Completed, false, null, rawPayment, false, rawPayment);

            if (value is "TRANSFER" or "BANK TRANSFER")
                return new RetailSettlement(OrderStatus.Completed, true, TransferCode, "Bank Transfer", false, rawPayment);

            // "NOT PAID" is a status, not a method: goods handed over, balance outstanding.
            if (value is "NOT PAID" or "UNPAID")
                return new RetailSettlement(OrderStatus.Served, false, null, null, false, rawPayment);

            // "CASH" states the method; "PAID" states only that the money was collected. Under
            // the confirmed ruling both settle in cash.
            if (value is "CASH" or "PAID")
                return new RetailSettlement(OrderStatus.Completed, true, CashCode, CashName, false, rawPayment);

            return new RetailSettlement(OrderStatus.Served, false, null, null, true, rawPayment);
        }

        /// <summary>A gift hands the goods over without collecting money, so it is completed
        /// and unsettled. Recognised on the order too, where the marker is the stored
        /// <c>Order.PaymentMethod</c> rather than the workbook cell.</summary>
        public static bool IsGift(string? value)
            => (value ?? string.Empty).TrimStart().StartsWith(GiftMarker, StringComparison.OrdinalIgnoreCase);
    }
}
