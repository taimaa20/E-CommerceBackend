using System.Text;
using RestaurantPos.Api.Modules.Retail.Domain;

namespace RestaurantPos.Api.Modules.Retail.Import
{
    /// <summary>What the sync intends to do with one workbook row or entity.</summary>
    public enum RetailSyncAction
    {
        /// <summary>Already in the database and identical — nothing is written.</summary>
        Unchanged = 0,
        Create = 1,
        Update = 2,
        /// <summary>Refused: the row cannot be applied and is reported instead.</summary>
        Skip = 3,
        /// <summary>The workbook and the database disagree in a way the importer will not
        /// resolve on its own. Reported, never guessed.</summary>
        Conflict = 4
    }

    public sealed record RetailFieldChange(string Field, string? From, string? To)
    {
        public override string ToString() => $"{Field}: {From ?? "—"} → {To ?? "—"}";
    }

    /// <summary>A row the importer refused to apply, or a fact it wants on the record.</summary>
    public sealed record RetailImportIssue(string Sheet, int Row, string Reason);

    public sealed record RetailSupplierPlan(
        string WorkbookName,
        string ResolvedName,
        string? Country,
        RetailSyncAction Action,
        Guid? ExistingId,
        string? MatchedExistingName,
        int ReferencedByProducts,
        int ReferencedByPurchases);

    public sealed record RetailCategoryPlan(string Name, RetailSyncAction Action, Guid? ExistingId, int ProductCount);

    public sealed record RetailProductPlan(
        int Row,
        string Sku,
        string? Name,
        RetailSyncAction Action,
        Guid? ProductId,
        IReadOnlyList<RetailFieldChange> Changes,
        string? Reason);

    public sealed record RetailSalePlan(
        int Row,
        string Sku,
        DateTime? Date,
        int Quantity,
        decimal UnitPrice,
        decimal LineTotal,
        RetailSyncAction Action,
        string OrderNumber,
        Guid? OrderId,
        IReadOnlyList<RetailFieldChange> Changes,
        string? Reason);

    /// <summary>An imported sale whose workbook row no longer exists. Never deleted — the
    /// workbook is not the authority on what the shop actually sold.</summary>
    public sealed record RetailOrphanSale(string OrderNumber, string Sku, DateTime Date, decimal Amount);

    public sealed record RetailPurchasePlan(
        int Row,
        string Key,
        string WorkbookNumber,
        RetailSyncAction Action,
        Guid? ExistingId,
        IReadOnlyList<RetailFieldChange> Changes,
        string? Reason);

    /// <summary>What has to change about a historical payment to satisfy the confirmed
    /// settlement ruling. Metadata only — never an amount, never a stock movement.</summary>
    public enum RetailPaymentFixKind
    {
        /// <summary>A settled sale with no Payment row at all. Settled in cash.</summary>
        MissingSettlement = 0,
        /// <summary>A Payment row whose method is missing or unusable. Restated as cash.</summary>
        UnreliableMethod = 1,
        /// <summary>A Payment stamped with the date the importer ran rather than the date the
        /// sale happened.</summary>
        TransactionDate = 2
    }

    public sealed record RetailPaymentFix(
        string OrderNumber,
        RetailPaymentFixKind Kind,
        decimal Amount,
        string Detail);

    public sealed record RetailStockPlan(
        string Sku,
        RetailStockMovementType MovementType,
        decimal Quantity,
        decimal? UnitCost,
        string Reason);

    public sealed record RetailInventoryCheck(
        string Sku,
        decimal ExpectedOnHand,
        decimal WorkbookOnHand,
        string? Explanation)
    {
        public decimal Variance => ExpectedOnHand - WorkbookOnHand;
        public bool IsMatch => Variance == 0m;
    }

    /// <summary>One settlement class of the workbook's historical sales, as the database holds
    /// them. The classes are exhaustive and disjoint, so they sum back to every imported order.</summary>
    public sealed record RetailPaymentSummaryRow(
        string Classification,
        int Orders,
        decimal OrderValue,
        int PaymentRows,
        decimal PaymentValue);

    public sealed record RetailProfitCheck(
        decimal WorkbookUnits,
        decimal WorkbookSales,
        decimal WorkbookCost,
        decimal WorkbookProfit,
        decimal SystemUnits,
        decimal SystemSales,
        decimal SystemCost,
        decimal SystemProfit);

    public sealed class RetailEntityTally
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Unchanged { get; set; }
        public int Skipped { get; set; }
        public int Conflicts { get; set; }

        public void Count(RetailSyncAction action)
        {
            switch (action)
            {
                case RetailSyncAction.Create: Created++; break;
                case RetailSyncAction.Update: Updated++; break;
                case RetailSyncAction.Unchanged: Unchanged++; break;
                case RetailSyncAction.Skip: Skipped++; break;
                case RetailSyncAction.Conflict: Conflicts++; break;
            }
        }

        public override string ToString()
            => $"new {Created}, changed {Updated}, unchanged {Unchanged}, skipped {Skipped}, conflicts {Conflicts}";
    }

    /// <summary>
    /// Everything the sync will do, decided before a single row is written. In dry-run mode
    /// this is the whole output; in a real run the same object is returned after the plan has
    /// been applied, so the report and the database can never describe different work.
    /// </summary>
    public sealed class RetailWorkbookImportResult
    {
        public string WorkbookPath { get; set; } = string.Empty;
        public Guid TenantId { get; set; }
        public Guid RetailBranchId { get; set; }
        public string RetailBranchCode { get; set; } = string.Empty;
        public string RetailBranchName { get; set; } = string.Empty;
        public bool IsDryRun { get; set; }

        public List<string> SheetsRead { get; } = new();
        public List<string> SheetsMissing { get; } = new();
        public List<string> ColumnsNotImported { get; } = new();

        public List<RetailSupplierPlan> Suppliers { get; } = new();
        public List<RetailCategoryPlan> Categories { get; } = new();
        public List<RetailProductPlan> Products { get; } = new();
        public List<RetailSalePlan> Sales { get; } = new();
        public List<RetailOrphanSale> OrphanSales { get; } = new();
        public List<RetailPurchasePlan> LegacyPurchases { get; } = new();
        public List<RetailStockPlan> StockPostings { get; } = new();
        public List<RetailPaymentFix> PaymentFixes { get; } = new();
        public List<RetailPaymentSummaryRow> PaymentSummary { get; } = new();
        public List<RetailInventoryCheck> InventoryChecks { get; } = new();
        public RetailProfitCheck? Profit { get; set; }

        public RetailEntityTally PaymentMethods { get; } = new();
        public RetailEntityTally BranchAssignments { get; } = new();
        public RetailEntityTally UserBranchAssignments { get; } = new();

        public decimal PreSyncOnHand { get; set; }
        public decimal ExpectedOnHand { get; set; }
        public decimal AppliedOnHand { get; set; }
        public int PreSyncMovements { get; set; }
        public int AppliedMovements { get; set; }
        public int OrdersReconciled { get; set; }
        public int PaymentsSettled { get; set; }
        public int PaymentMethodsRestated { get; set; }
        public int PaymentDatesCorrected { get; set; }

        /// <summary>Workbook rows or database rows a person has to look at.</summary>
        public List<RetailImportIssue> Reviews { get; } = new();
        public List<RetailImportIssue> Issues { get; } = new();

        public void AddIssue(string sheet, int row, string reason) => Issues.Add(new RetailImportIssue(sheet, row, reason));
        public void AddReview(string sheet, int row, string reason) => Reviews.Add(new RetailImportIssue(sheet, row, reason));

        public RetailEntityTally Tally<T>(IEnumerable<T> plans, Func<T, RetailSyncAction> selector)
        {
            var tally = new RetailEntityTally();
            foreach (var plan in plans)
                tally.Count(selector(plan));
            return tally;
        }

        public RetailEntityTally SupplierTally => Tally(Suppliers, p => p.Action);
        public RetailEntityTally CategoryTally => Tally(Categories, p => p.Action);
        public RetailEntityTally ProductTally => Tally(Products, p => p.Action);
        public RetailEntityTally SaleTally => Tally(Sales, p => p.Action);
        public RetailEntityTally PurchaseTally => Tally(LegacyPurchases, p => p.Action);

        public bool HasBlockingConflicts =>
            Products.Any(p => p.Action == RetailSyncAction.Conflict)
            || Sales.Any(s => s.Action == RetailSyncAction.Conflict)
            || LegacyPurchases.Any(p => p.Action == RetailSyncAction.Conflict);

        public string ToReport()
        {
            var report = new StringBuilder();
            var mode = IsDryRun ? "DRY RUN — no database writes" : "APPLIED";

            Header(report, $"DOHA LUXE retail workbook sync — {mode}");
            report.AppendLine($" Workbook      : {WorkbookPath}");
            report.AppendLine($" Target branch : {RetailBranchName} [{RetailBranchCode}] {RetailBranchId}");
            report.AppendLine($" Tenant        : {TenantId}");
            report.AppendLine($" Sheets read   : {string.Join(", ", SheetsRead)}");
            if (SheetsMissing.Count > 0)
                report.AppendLine($" Sheets absent : {string.Join(", ", SheetsMissing)}");
            report.AppendLine();

            Header(report, "1. Reconciliation");
            report.AppendLine($" Suppliers          : {SupplierTally}");
            report.AppendLine($" Categories         : {CategoryTally}");
            report.AppendLine($" Products           : {ProductTally}");
            report.AppendLine($" Historical sales   : {SaleTally}");
            report.AppendLine($" Legacy purchases   : {PurchaseTally}");
            report.AppendLine($" Sales in the database with no workbook row : {OrphanSales.Count}");
            report.AppendLine();

            Detail(report, "2. Suppliers", Suppliers.Where(s => s.Action != RetailSyncAction.Unchanged),
                s => $"{s.WorkbookName} → {s.ResolvedName}{(s.MatchedExistingName is null ? string.Empty : $" (matched '{s.MatchedExistingName}')")} [{s.Action}] used by {s.ReferencedByProducts} product(s), {s.ReferencedByPurchases} purchase(s)");

            Detail(report, "3. Categories", Categories.Where(c => c.Action != RetailSyncAction.Unchanged),
                c => $"{c.Name} [{c.Action}] {c.ProductCount} product(s)");

            Detail(report, "4. Products changed or refused", Products.Where(p => p.Action is RetailSyncAction.Update or RetailSyncAction.Skip or RetailSyncAction.Conflict),
                p => $"row {p.Row} {p.Sku} [{p.Action}] {p.Reason ?? string.Join("; ", p.Changes)}");

            Detail(report, "5. Products created", Products.Where(p => p.Action == RetailSyncAction.Create),
                p => $"row {p.Row} {p.Sku} — {p.Name}");

            Detail(report, "6. Historical sales created", Sales.Where(s => s.Action == RetailSyncAction.Create),
                s => $"row {s.Row} {s.OrderNumber} {s.Date:yyyy-MM-dd} {s.Sku} x{s.Quantity} @ {s.UnitPrice:0.00} = {s.LineTotal:0.00}");

            Detail(report, "7. Historical sales updated", Sales.Where(s => s.Action is RetailSyncAction.Update or RetailSyncAction.Conflict),
                s => $"row {s.Row} {s.OrderNumber} [{s.Action}] {s.Reason ?? string.Join("; ", s.Changes)}");

            Detail(report, "8. Sales present in the database but absent from the new workbook", OrphanSales,
                o => $"{o.OrderNumber} {o.Date:yyyy-MM-dd} {o.Sku} {o.Amount:0.00} — kept, not deleted");

            Detail(report, "9. Legacy purchase orders", LegacyPurchases.Where(p => p.Action != RetailSyncAction.Unchanged),
                p => $"row {p.Row} '{p.Key}' [{p.Action}] {p.Reason ?? string.Join("; ", p.Changes)}");

            Header(report, "10. Stock ledger");
            report.AppendLine($" Pre-sync retail stock on hand   : {PreSyncOnHand:0.##} over {PreSyncMovements} movement(s)");
            foreach (var group in StockPostings.GroupBy(s => s.MovementType))
                report.AppendLine($" {group.Key,-20} : {group.Count()} posting(s), {group.Sum(s => RetailStockMovement.SignedQuantity(s.MovementType, s.Quantity)):+0.##;-0.##;0} units");
            report.AppendLine($" New historical sale movements   : {Sales.Count(s => s.Action == RetailSyncAction.Create)} (posted by the order reconciler)");
            report.AppendLine($" Expected post-sync stock on hand: {ExpectedOnHand:0.##}");
            if (!IsDryRun)
            {
                report.AppendLine($" Actual post-sync stock on hand  : {AppliedOnHand:0.##}");
                report.AppendLine($" Movements written               : {AppliedMovements}");
                report.AppendLine($" Orders reconciled               : {OrdersReconciled}");
            }
            report.AppendLine();

            Detail(report, "11. Workbook-origin stock corrections", StockPostings.Where(s => s.MovementType is RetailStockMovementType.AdjustmentIncrease or RetailStockMovementType.AdjustmentDecrease),
                s => $"{s.Sku}: {RetailStockMovement.SignedQuantity(s.MovementType, s.Quantity):+0.##;-0.##} — {s.Reason}");

            Header(report, "11b. Historical payment normalisation (PAID = CASH · CASH = CASH · NOT PAID = unpaid)");
            foreach (var kind in Enum.GetValues<RetailPaymentFixKind>())
            {
                var fixes = PaymentFixes.Where(f => f.Kind == kind).ToList();
                report.AppendLine($" {kind,-18} : {fixes.Count} payment(s), {fixes.Sum(f => f.Amount):N2}");
            }
            foreach (var fix in PaymentFixes.Where(f => f.Kind != RetailPaymentFixKind.TransactionDate))
                report.AppendLine($"   {fix.OrderNumber} [{fix.Kind}] {fix.Detail}");
            if (PaymentFixes.Count == 0)
                report.AppendLine("   nothing to correct — historical payments already satisfy the ruling");
            if (!IsDryRun)
            {
                report.AppendLine($" applied             : {PaymentsSettled} settled in cash, {PaymentMethodsRestated} method(s) restated, {PaymentDatesCorrected} date(s) moved to the sale date");
            }
            report.AppendLine();

            if (PaymentSummary.Count > 0)
            {
                Header(report, "11c. Historical payment reconciliation");
                report.AppendLine($" {"class",-46}{"orders",8}{"value",12}{"pay rows",10}{"pay value",12}");
                foreach (var row in PaymentSummary)
                    report.AppendLine($" {row.Classification,-46}{row.Orders,8}{row.OrderValue,12:N2}{row.PaymentRows,10}{row.PaymentValue,12:N2}");
                report.AppendLine($" {"TOTAL",-46}{PaymentSummary.Sum(r => r.Orders),8}{PaymentSummary.Sum(r => r.OrderValue),12:N2}{PaymentSummary.Sum(r => r.PaymentRows),10}{PaymentSummary.Sum(r => r.PaymentValue),12:N2}");
                report.AppendLine();
            }

            var mismatches = InventoryChecks.Where(c => !c.IsMatch).ToList();
            Header(report, "12. LIVE INVENTORY verification");
            report.AppendLine($" SKUs compared        : {InventoryChecks.Count}");
            report.AppendLine($" Matching             : {InventoryChecks.Count - mismatches.Count}");
            report.AppendLine($" Mismatched           : {mismatches.Count}");
            report.AppendLine($" Ledger total         : {InventoryChecks.Sum(c => c.ExpectedOnHand):0.##}");
            report.AppendLine($" Workbook total       : {InventoryChecks.Sum(c => c.WorkbookOnHand):0.##}");
            foreach (var check in mismatches)
                report.AppendLine($"   {check.Sku}: ledger {check.ExpectedOnHand:0.##} vs workbook {check.WorkbookOnHand:0.##} ({check.Variance:+0.##;-0.##}) — {check.Explanation ?? "unexplained"}");
            report.AppendLine();

            if (Profit is { } profit)
            {
                Header(report, "13. SUMMARY PROFIT verification");
                report.AppendLine($" {"",-22}{"workbook",14}{"system",14}{"difference",14}");
                Money(report, "Units sold", profit.WorkbookUnits, profit.SystemUnits);
                Money(report, "Sales", profit.WorkbookSales, profit.SystemSales);
                Money(report, "Cost of goods", profit.WorkbookCost, profit.SystemCost);
                Money(report, "Profit", profit.WorkbookProfit, profit.SystemProfit);
                report.AppendLine(" Costing basis differs by design: the workbook multiplies units sold by TODAY'S");
                report.AppendLine(" cost per item, the system uses the cost snapshotted at the moment of each sale.");
                report.AppendLine();
            }

            if (ColumnsNotImported.Count > 0)
            {
                Header(report, "14. Workbook columns deliberately not imported");
                foreach (var column in ColumnsNotImported)
                    report.AppendLine($"   {column}");
                report.AppendLine();
            }

            Detail(report, "15. Requires manual review", Reviews, r => $"[{r.Sheet}] row {r.Row}: {r.Reason}");
            Detail(report, "16. Rows reported", Issues, i => $"[{i.Sheet}] row {i.Row}: {i.Reason}");

            report.AppendLine(new string('─', 78));
            return report.ToString();
        }

        private static void Header(StringBuilder report, string title)
        {
            report.AppendLine(new string('─', 78));
            report.AppendLine($" {title}");
            report.AppendLine(new string('─', 78));
        }

        private static void Detail<T>(StringBuilder report, string title, IEnumerable<T> rows, Func<T, string> render)
        {
            var list = rows.ToList();
            Header(report, $"{title} ({list.Count})");
            foreach (var row in list)
                report.AppendLine($"   {render(row)}");
            if (list.Count == 0)
                report.AppendLine("   none");
            report.AppendLine();
        }

        private static void Money(StringBuilder report, string label, decimal workbook, decimal system)
            => report.AppendLine($" {label,-22}{workbook,14:N2}{system,14:N2}{system - workbook,14:N2}");
    }
}
