using RestaurantPos.Api.Modules.Dashboard.DTOs.Inventory;
using RestaurantPos.Api.Modules.Dashboard.Services.Export;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Inventory
{
    /// <summary>
    /// Bilingual Excel export for the Inventory dashboard. Consumes the same
    /// <see cref="InventorySnapshotDto"/> the page renders — stock health, the
    /// canonical waste breakdown, top-wasted, by-employee, trend, expiring and
    /// by-category — so the workbook reconciles with the screen to the cent.
    /// </summary>
    public sealed class InventoryDashboardExportService : IInventoryDashboardExportService
    {
        public DashboardExportFile BuildWorkbook(InventorySnapshotDto snapshot, string? currency)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var cur = NormalizeCurrency(currency);
            var s = snapshot.StockHealth;
            var w = snapshot.Waste;

            return DashboardWorkbookWriter.Build("inventory-dashboard", workbook =>
            {
                DashboardWorkbookWriter.OverviewSheet(workbook, "Overview - الملخص", snapshot, new (string, object?, string?)[]
                {
                    ("Inventory value / قيمة المخزون", s.TotalInventoryValue, cur),
                    ("Out of stock / نفد المخزون", s.OutOfStockCount, null),
                    ("Low stock / مخزون منخفض", s.LowStockCount, null),
                    ("Reorder needed / يحتاج إعادة طلب", s.ReorderNeededCount, null),
                    ("Expiring within 3 days / ينتهي خلال 3 أيام", s.ExpiringWithin3Days, null),
                    ("Waste cost / تكلفة الهدر", w.TotalCost, cur),
                    ("Waste sale loss / خسارة البيع", w.SalePriceLoss, cur),
                    ("Waste % of revenue / نسبة الهدر", w.WastePercentOfRevenue, "%"),
                    ("Manual waste records / هدر يدوي", w.ManualCount, null),
                    ("Expiry waste records / هدر انتهاء", w.ExpiryCount, null),
                    ("Cancellation waste records / هدر إلغاء", w.CancellationCount, null),
                });

                DashboardWorkbookWriter.RankedSheet(workbook, "Top Wasted - الأكثر هدراً",
                    snapshot.TopWastedProducts, "Cost / التكلفة", "Sale loss / خسارة البيع");
                DashboardWorkbookWriter.RankedSheet(workbook, "Waste by Employee - الموظفون",
                    snapshot.WasteByEmployee, "Cost / التكلفة", null);
                DashboardWorkbookWriter.RankedSheet(workbook, "Expiring Soon - قرب الانتهاء",
                    snapshot.ExpiringSoon, "Value / القيمة", "Quantity / الكمية");

                DashboardWorkbookWriter.TimeSeriesSheet(workbook, "Waste Trend - اتجاه الهدر",
                    "Date / التاريخ", "Cost / التكلفة", snapshot.WasteDailyTrend, dateOnly: true);

                DashboardWorkbookWriter.CategorySheet(workbook, "Waste by Category - حسب الفئة",
                    snapshot.WasteByCategory, "Cost / التكلفة");
            });
        }

        private static string NormalizeCurrency(string? currency)
        {
            var normalized = currency?.Trim().ToUpperInvariant();
            return normalized is { Length: 3 } && normalized.All(char.IsLetter) ? normalized : "USD";
        }
    }
}
