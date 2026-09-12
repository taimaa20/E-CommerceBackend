using RestaurantPos.Api.Modules.Dashboard.DTOs.Operations;
using RestaurantPos.Api.Modules.Dashboard.Services.Export;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Operational
{
    /// <summary>
    /// Bilingual Excel export for the Operations dashboard. Exports the windowed
    /// aggregate metrics the page shows (counters, revenue split, order types,
    /// hourly series, top products). The live pipeline table is real-time point
    /// state, not a windowed metric, so it is intentionally not exported.
    /// </summary>
    public sealed class OperationsDashboardExportService : IOperationsDashboardExportService
    {
        public DashboardExportFile BuildWorkbook(OperationsSnapshotDto snapshot, string? currency)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var cur = NormalizeCurrency(currency);
            var o = snapshot.Orders;
            var r = snapshot.Revenue;
            var hh = snapshot.Health;

            return DashboardWorkbookWriter.Build("operations-dashboard", workbook =>
            {
                DashboardWorkbookWriter.OverviewSheet(workbook, "Overview - الملخص", snapshot, new (string, object?, string?)[]
                {
                    ("Gross sales / إجمالي المبيعات", r.Gross, cur),
                    ("Discounts / الخصومات", r.Discounts, cur),
                    ("Net sales / صافي المبيعات", r.Net, cur),
                    ("Confirmed revenue / الإيراد المؤكد", r.Confirmed, cur),
                    ("Pending revenue / الإيراد المعلق", r.Pending, cur),
                    ("Cash / نقدي", r.Cash, cur),
                    ("Card / بطاقة", r.Card, cur),
                    ("Cash % / نسبة النقدي", r.CashPercentage, "%"),
                    ("Card % / نسبة البطاقة", r.CardPercentage, "%"),
                    ("Total orders / إجمالي الطلبات", o.Total, null),
                    ("Active / نشطة", o.Active, null),
                    ("Waiting / بالانتظار", o.Waiting, null),
                    ("Preparing / قيد التحضير", o.Preparing, null),
                    ("Ready / جاهزة", o.Ready, null),
                    ("Paid / مدفوعة", o.Paid, null),
                    ("Served / مقدمة", o.Served, null),
                    ("Cancelled / ملغاة", o.Cancelled, null),
                    ("Refunded / مستردة", o.Refunded, null),
                    ("Stale over threshold / متأخرة", o.StaleOverThreshold, null),
                    ("Average order / متوسط الطلب", o.AverageOrderValue, cur),
                    ("Occupied tables / الطاولات المشغولة", hh.ActiveDineInTables, null),
                    ("Kitchen queue / طابور المطبخ", hh.KitchenQueueLength, null),
                    ("Online orders live / طلبات أونلاين", hh.OnlineOrdersLive, null),
                    ("Active cashier sessions / جلسات الكاشير", hh.ActiveCashierSessions, null),
                    ("Average prep minutes / متوسط زمن التحضير", hh.PrepOrderCount > 0 ? hh.AveragePrepMinutes : (object?)null, "min"),
                });

                DashboardWorkbookWriter.TimeSeriesSheet(workbook, "Hourly - بالساعة",
                    "Hour / الساعة", "Revenue / الإيراد", snapshot.HourlySeries, dateOnly: false);

                DashboardWorkbookWriter.CategorySheet(workbook, "Order Types - أنواع الطلبات",
                    snapshot.OrderTypeSplit, "Revenue / الإيراد");
                DashboardWorkbookWriter.CategorySheet(workbook, "Payments - المدفوعات",
                    r.PaymentBreakdown, "Amount / المبلغ");

                DashboardWorkbookWriter.RankedSheet(workbook, "Top Products - المنتجات",
                    snapshot.TopProducts, "Revenue / الإيراد", null, "Quantity / الكمية");
            });
        }

        private static string NormalizeCurrency(string? currency)
        {
            var normalized = currency?.Trim().ToUpperInvariant();
            return normalized is { Length: 3 } && normalized.All(char.IsLetter) ? normalized : "USD";
        }
    }
}
