using RestaurantPos.Api.Modules.Dashboard.DTOs.Home;
using RestaurantPos.Api.Modules.Dashboard.Services.Export;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Home
{
    /// <summary>
    /// Bilingual Excel export for the Home (executive overview) dashboard.
    /// Consumes the exact same <see cref="HomeSnapshotDto"/> the page renders,
    /// so the workbook can never disagree with the on-screen numbers.
    /// </summary>
    public sealed class HomeDashboardExportService : IHomeDashboardExportService
    {
        public DashboardExportFile BuildWorkbook(HomeSnapshotDto snapshot, string? currency)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var cur = NormalizeCurrency(currency);
            var h = snapshot.Headline;

            return DashboardWorkbookWriter.Build("home-dashboard", workbook =>
            {
                DashboardWorkbookWriter.OverviewSheet(workbook, "Overview - الملخص", snapshot, new (string, object?, string?)[]
                {
                    ("Revenue / الإيراد", h.RevenueToday, cur),
                    ("Paid orders / الطلبات المدفوعة", h.OrdersToday, null),
                    ("Active orders / الطلبات النشطة", h.ActiveOrders, null),
                    ("Stale orders (>15 min) / الطلبات المتأخرة", h.StaleOrders, null),
                    ("Average order / متوسط الطلب", h.AverageOrderValue, cur),
                    ("Net profit / الربح الصافي", h.NetProfit, cur),
                    ("Profit margin / هامش الربح", h.MarginPercent, "%"),
                    ("Waste cost / تكلفة الهدر", h.WasteCost, cur),
                    ("Waste % of revenue / نسبة الهدر", h.WastePercent, "%"),
                    ("Active tables / الطاولات المشغولة", h.ActiveTables, null),
                    ("Total tables / إجمالي الطاولات", h.TotalTables, null),
                });

                DashboardWorkbookWriter.TimeSeriesSheet(workbook, "Revenue Trend - الإيراد",
                    "Date / التاريخ", "Revenue / الإيراد", snapshot.RevenueTrend, dateOnly: true);
                DashboardWorkbookWriter.TimeSeriesSheet(workbook, "Hourly Orders - الطلبات بالساعة",
                    "Hour / الساعة", "Revenue / الإيراد", snapshot.HourlyOrders, dateOnly: false);

                DashboardWorkbookWriter.RankedSheet(workbook, "Top Products - المنتجات",
                    snapshot.TopProducts, "Revenue / الإيراد", null, "Quantity / الكمية");
                DashboardWorkbookWriter.RankedSheet(workbook, "Top Cashiers - الكاشير",
                    snapshot.TopCashiers, "Revenue / الإيراد", null);

                DashboardWorkbookWriter.CategorySheet(workbook, "Payments - المدفوعات",
                    snapshot.PaymentSplit, "Amount / المبلغ");
                DashboardWorkbookWriter.CategorySheet(workbook, "Order Types - أنواع الطلبات",
                    snapshot.OrderTypeSplit, "Orders / الطلبات", valueIsMoney: false, countHeader: null);
            });
        }

        private static string NormalizeCurrency(string? currency)
        {
            var normalized = currency?.Trim().ToUpperInvariant();
            return normalized is { Length: 3 } && normalized.All(char.IsLetter) ? normalized : "USD";
        }
    }
}
