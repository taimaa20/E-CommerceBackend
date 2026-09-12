using ClosedXML.Excel;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Common;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Financial;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Financial
{
    public sealed class FinancialDashboardExportService : IFinancialDashboardExportService
    {
        private const string DefaultCurrency = "USD";
        private const string WorkbookContentType =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public FinancialDashboardExportFile BuildWorkbook(
            FinancialSnapshotDto snapshot,
            string? currency,
            CommissionAnalysisDto? commission = null)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            using var workbook = new XLWorkbook();
            AddOverviewSheet(workbook, snapshot, NormalizeCurrency(currency));
            AddDailyRevenueSheet(workbook, snapshot.RevenueByDay);
            AddCategorySheet(workbook, "Payments - المدفوعات", snapshot.ChannelSplit);
            AddRankedSheet(workbook, "Cashiers - الكاشير", snapshot.RevenueByCashier, "Revenue / الإيراد");
            AddRankedSheet(workbook, "Discounts - الخصومات", snapshot.DiscountsByUser, "Discounts / الخصومات");
            AddProductsSheet(workbook, snapshot);
            AddPaymentAnalyticsSheet(workbook, snapshot.PaymentMethodAnalytics);
            AddOrderSourceAnalyticsSheet(workbook, snapshot.OrderSourceAnalytics);
            AddPartnerAnalyticsSheet(workbook, snapshot.PartnerAnalytics);
            if (snapshot.CardBreakdown.Count > 0)
                AddCategorySheet(workbook, "Cards - البطاقات", snapshot.CardBreakdown);
            if (snapshot.PayMobBreakdown.Count > 0)
                AddCategorySheet(workbook, "PayMob - باي موب", snapshot.PayMobBreakdown);
            if (commission is not null)
                AddCommissionSheets(workbook, commission, NormalizeCurrency(currency));

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileName = $"financial-dashboard-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
            return new FinancialDashboardExportFile(stream.ToArray(), fileName, WorkbookContentType);
        }

        private static void AddOverviewSheet(
            XLWorkbook workbook,
            FinancialSnapshotDto snapshot,
            string currency)
        {
            var sheet = workbook.Worksheets.Add("Overview - الملخص");
            WriteHeaders(sheet, "Metric / المؤشر", "Value / القيمة", "Unit / الوحدة");

            var row = 2;
            AddMetric(sheet, ref row, "Period start / بداية الفترة", snapshot.WindowStartUtc);
            AddMetric(sheet, ref row, "Period end / نهاية الفترة", snapshot.WindowEndUtc);
            AddMetric(sheet, ref row, "Generated / تاريخ الإنشاء", snapshot.GeneratedAtUtc);
            AddMetric(sheet, ref row, "Gross revenue / الإيراد الإجمالي", snapshot.Revenue.Gross, currency);
            AddMetric(sheet, ref row, "Discounts / الخصومات", snapshot.Revenue.Discounts, currency);
            AddMetric(sheet, ref row, "Discount rate / نسبة الخصم", snapshot.Revenue.DiscountPercentage, "%");
            AddMetric(sheet, ref row, "Vouchers / القسائم", snapshot.Revenue.Vouchers, currency);
            AddMetric(sheet, ref row, "Refunds / المرتجعات", snapshot.Revenue.Refunds, currency);
            AddMetric(sheet, ref row, "Net revenue / صافي الإيراد", snapshot.Revenue.Net, currency);
            AddMetric(sheet, ref row, "Tax / الضريبة", snapshot.Revenue.Tax, currency);
            AddMetric(sheet, ref row, "Service charge / رسوم الخدمة", snapshot.Revenue.ServiceCharge, currency);
            AddMetric(sheet, ref row, "Orders / الطلبات", snapshot.Revenue.OrderCount);
            AddMetric(sheet, ref row, "Average order / متوسط الطلب", snapshot.Revenue.AverageOrderValue, currency);
            AddMetric(sheet, ref row, "Cancelled before payment / ملغاة قبل الدفع", snapshot.Cancellations.CancelledBeforePaymentCount);
            AddMetric(sheet, ref row, "Cancelled value / قيمة الإلغاءات", snapshot.Cancellations.CancelledBeforePaymentAmount, currency);
            AddMetric(sheet, ref row, "Refunded orders / الطلبات المستردة", snapshot.Cancellations.RefundedOrderCount);
            AddMetric(sheet, ref row, "Refunded amount / قيمة المرتجعات", snapshot.Cancellations.RefundedAmount, currency);
            AddMetric(sheet, ref row, "COGS / تكلفة البضاعة", snapshot.Profitability.Cogs, currency);
            AddMetric(sheet, ref row, "Labour / العمالة", snapshot.Profitability.LaborCost, currency);
            AddMetric(sheet, ref row, "Prime cost / التكلفة الأولية", snapshot.Profitability.PrimeCost, currency);
            AddMetric(sheet, ref row, "Prime cost % / نسبة التكلفة الأولية", snapshot.Profitability.PrimeCostPercentage, "%");
            AddMetric(sheet, ref row, "Gross profit / الربح الإجمالي", snapshot.Profitability.GrossProfit, currency);
            AddMetric(sheet, ref row, "Gross margin / الهامش الإجمالي", snapshot.Profitability.GrossMarginPercent, "%");
            AddMetric(sheet, ref row, "Net profit / الربح الصافي", snapshot.Profitability.NetProfit, currency);
            AddMetric(sheet, ref row, "Profit margin / هامش الربح", snapshot.Profitability.MarginPercent, "%");
            AddMetric(sheet, ref row, "Profit per order / الربح لكل طلب", snapshot.Profitability.ProfitPerOrder, currency);
            AddMetric(sheet, ref row, "Actual food cost / تكلفة الطعام الفعلية", snapshot.FoodCost.ActualFoodCost, currency);
            AddMetric(sheet, ref row, "Food cost % / نسبة تكلفة الطعام", snapshot.FoodCost.FoodCostPercentage, "%");
            AddMetric(sheet, ref row, "Theoretical food cost / التكلفة النظرية", snapshot.FoodCost.TheoreticalFoodCost, currency);
            AddMetric(sheet, ref row, "Food cost variance / فرق تكلفة الطعام", snapshot.FoodCost.FoodCostVariance, currency);
            AddMetric(sheet, ref row, "Purchasing cost / تكلفة المشتريات", snapshot.Purchasing.TotalPurchasingCost, currency);
            AddMetric(sheet, ref row, "Purchase orders / أوامر الشراء", snapshot.Purchasing.PurchaseOrderCount);
            AddMetric(sheet, ref row, "Labour cost / تكلفة العمالة", snapshot.LaborCost.LaborCost, currency);
            AddMetric(sheet, ref row, "Labour % / نسبة العمالة", snapshot.LaborCost.LaborCostPercentage, "%");
            AddMetric(sheet, ref row, "Revenue per labour hour / الإيراد لكل ساعة عمل", snapshot.LaborCost.RevenuePerLaborHour, currency);
            AddMetric(sheet, ref row, "Manual discounts / خصومات يدوية", snapshot.DiscountCost.ManualDiscounts, currency);
            AddMetric(sheet, ref row, "Voucher discounts / قسائم", snapshot.DiscountCost.VoucherDiscounts, currency);
            AddMetric(sheet, ref row, "Complimentary value / أصناف مجانية", snapshot.DiscountCost.ComplimentaryValue, currency);
            AddMetric(sheet, ref row, "Partner discounts / خصومات الشركاء", snapshot.DiscountCost.PartnerDiscounts, currency);
            AddMetric(sheet, ref row, "Total items sold / إجمالي الأصناف", snapshot.SalesMix.TotalItems);
            AddMetric(sheet, ref row, "Avg items per order / متوسط الأصناف للطلب", snapshot.SalesMix.AverageItemsPerOrder);
            AddMetric(sheet, ref row, "Delivery revenue / إيراد التوصيل", snapshot.Delivery.Revenue, currency);
            AddMetric(sheet, ref row, "Delivery cost / تكلفة التوصيل", snapshot.Delivery.Cost, currency);
            AddMetric(sheet, ref row, "Delivery profit / ربح التوصيل", snapshot.Delivery.Profit, currency);
            AddMetric(sheet, ref row, "Total commission / إجمالي العمولة", snapshot.CostSharing.TotalCommission, currency);
            AddMetric(sheet, ref row, "Restaurant commission share / حصة المطعم من العمولة", snapshot.CostSharing.CommissionPaidByRestaurant, currency);
            AddMetric(sheet, ref row, "Partner/card commission share / حصة الشريك أو البطاقة من العمولة", snapshot.CostSharing.CommissionPaidByCounterparty, currency);
            AddMetric(sheet, ref row, "Net revenue after cost sharing / صافي الإيراد بعد تقاسم التكلفة", snapshot.CostSharing.NetRevenueAfterCostSharing, currency);

            FormatSheet(sheet);
        }

        private static void AddDailyRevenueSheet(
            XLWorkbook workbook,
            IReadOnlyList<TimeSeriesPointDto> rows)
        {
            var sheet = workbook.Worksheets.Add("Daily Revenue - الإيراد");
            WriteHeaders(sheet, "Date / التاريخ", "Revenue / الإيراد", "Orders / الطلبات");

            for (var index = 0; index < rows.Count; index++)
            {
                var row = index + 2;
                sheet.Cell(row, 1).Value = rows[index].BucketStartUtc;
                sheet.Cell(row, 2).Value = rows[index].Value;
                sheet.Cell(row, 3).Value = rows[index].Count ?? 0;
            }

            sheet.Column(1).Style.DateFormat.Format = "yyyy-mm-dd";
            sheet.Column(2).Style.NumberFormat.Format = "#,##0.00";
            FormatSheet(sheet);
        }

        private static void AddCategorySheet(
            XLWorkbook workbook,
            string sheetName,
            IReadOnlyList<CategorySliceDto> rows)
        {
            var sheet = workbook.Worksheets.Add(sheetName);
            WriteHeaders(
                sheet,
                "Name (English) / الاسم بالإنجليزية",
                "Name (Arabic) / الاسم بالعربية",
                "Amount / المبلغ",
                "Percentage / النسبة");

            for (var index = 0; index < rows.Count; index++)
            {
                var row = index + 2;
                sheet.Cell(row, 1).Value = rows[index].Label;
                sheet.Cell(row, 2).Value = rows[index].LabelAr ?? rows[index].Label;
                sheet.Cell(row, 3).Value = rows[index].Value;
                sheet.Cell(row, 4).Value = rows[index].Percentage ?? 0;
            }

            sheet.Column(3).Style.NumberFormat.Format = "#,##0.00";
            sheet.Column(4).Style.NumberFormat.Format = "0.00";
            FormatSheet(sheet);
        }

        private static void AddRankedSheet(
            XLWorkbook workbook,
            string sheetName,
            IReadOnlyList<RankedRowDto> rows,
            string valueHeader)
        {
            var sheet = workbook.Worksheets.Add(sheetName);
            WriteHeaders(
                sheet,
                "Name (English) / الاسم بالإنجليزية",
                "Name (Arabic) / الاسم بالعربية",
                valueHeader,
                "Count / العدد");

            WriteRankedRows(sheet, rows, 2, null);
            sheet.Column(3).Style.NumberFormat.Format = "#,##0.00";
            FormatSheet(sheet);
        }

        private static void AddProductsSheet(
            XLWorkbook workbook,
            FinancialSnapshotDto snapshot)
        {
            var sheet = workbook.Worksheets.Add("Products - المنتجات");
            WriteHeaders(
                sheet,
                "Section / القسم",
                "Name (English) / الاسم بالإنجليزية",
                "Name (Arabic) / الاسم بالعربية",
                "Revenue / الإيراد",
                "Quantity / الكمية");

            var nextRow = WriteRankedRows(sheet, snapshot.BestSellers, 2, "Best sellers / الأكثر مبيعاً");
            WriteRankedRows(sheet, snapshot.WorstSellers, nextRow, "Review / للمراجعة");
            sheet.Column(4).Style.NumberFormat.Format = "#,##0.00";
            FormatSheet(sheet);
        }

        private static void AddPaymentAnalyticsSheet(
            XLWorkbook workbook,
            IReadOnlyList<PaymentMethodAnalyticsDto> rows)
        {
            var sheet = workbook.Worksheets.Add("Payment Analytics - الدفع");
            WriteHeaders(sheet,
                "Payment method / طريقة الدفع",
                "Arabic name / الاسم بالعربية",
                "Status / الحالة",
                "Orders / الطلبات",
                "Refunded orders / الطلبات المستردة",
                "Gross revenue / الإيراد الإجمالي",
                "Refunds / المرتجعات",
                "Discounts / الخصومات",
                "Net revenue / صافي الإيراد",
                "Total commission / إجمالي العمولة",
                "Restaurant share / حصة المطعم",
                "Partner/card share / حصة الشريك أو البطاقة",
                "Net settlement / صافي التسوية",
                "Net after cost sharing / الصافي بعد تقاسم التكلفة",
                "Average order / متوسط الطلب",
                "Revenue % / نسبة الإيراد");

            for (var index = 0; index < rows.Count; index++)
            {
                var row = index + 2;
                sheet.Cell(row, 1).Value = rows[index].Name;
                sheet.Cell(row, 2).Value = rows[index].NameAr ?? rows[index].Name;
                sheet.Cell(row, 3).Value = rows[index].IsActive ? "Active / نشط" : "Inactive / غير نشط";
                sheet.Cell(row, 4).Value = rows[index].TotalOrders;
                sheet.Cell(row, 5).Value = rows[index].RefundedOrderCount;
                sheet.Cell(row, 6).Value = rows[index].GrossRevenue;
                sheet.Cell(row, 7).Value = rows[index].RefundAmount;
                sheet.Cell(row, 8).Value = rows[index].DiscountAmount;
                sheet.Cell(row, 9).Value = rows[index].NetRevenue;
                sheet.Cell(row, 10).Value = rows[index].TotalCommission;
                sheet.Cell(row, 11).Value = rows[index].RestaurantShare;
                sheet.Cell(row, 12).Value = rows[index].CounterpartyShare;
                sheet.Cell(row, 13).Value = rows[index].NetSettlement;
                sheet.Cell(row, 14).Value = rows[index].NetRevenueAfterCostSharing;
                sheet.Cell(row, 15).Value = rows[index].AverageOrderValue;
                sheet.Cell(row, 16).Value = rows[index].RevenuePercentage;
            }

            sheet.Columns(6, 15).Style.NumberFormat.Format = "#,##0.00";
            sheet.Column(16).Style.NumberFormat.Format = "0.00";
            FormatSheet(sheet);
        }

        private static void AddOrderSourceAnalyticsSheet(
            XLWorkbook workbook,
            IReadOnlyList<OrderSourceAnalyticsDto> rows)
        {
            var sheet = workbook.Worksheets.Add("Order Sources - المصادر");
            WriteHeaders(sheet,
                "Order source / مصدر الطلب",
                "Arabic name / الاسم بالعربية",
                "Orders / الطلبات",
                "Gross revenue / الإيراد الإجمالي",
                "Net revenue / صافي الإيراد",
                "Refunded orders / الطلبات المستردة",
                "Refunds / المرتجعات",
                "Cancelled before payment / ملغاة قبل الدفع",
                "Cancelled value / قيمة الإلغاءات",
                "Average order / متوسط الطلب");

            for (var index = 0; index < rows.Count; index++)
            {
                var row = index + 2;
                sheet.Cell(row, 1).Value = rows[index].Name;
                sheet.Cell(row, 2).Value = rows[index].NameAr;
                sheet.Cell(row, 3).Value = rows[index].OrderCount;
                sheet.Cell(row, 4).Value = rows[index].GrossRevenue;
                sheet.Cell(row, 5).Value = rows[index].NetRevenue;
                sheet.Cell(row, 6).Value = rows[index].RefundedOrderCount;
                sheet.Cell(row, 7).Value = rows[index].RefundAmount;
                sheet.Cell(row, 8).Value = rows[index].CancelledBeforePaymentCount;
                sheet.Cell(row, 9).Value = rows[index].CancelledBeforePaymentAmount;
                sheet.Cell(row, 10).Value = rows[index].AverageOrderValue;
            }

            sheet.Columns(4, 5).Style.NumberFormat.Format = "#,##0.00";
            sheet.Column(7).Style.NumberFormat.Format = "#,##0.00";
            sheet.Columns(9, 10).Style.NumberFormat.Format = "#,##0.00";
            FormatSheet(sheet);
        }

        private static void AddPartnerAnalyticsSheet(
            XLWorkbook workbook,
            IReadOnlyList<PartnerAnalyticsDto> rows)
        {
            var sheet = workbook.Worksheets.Add("Partners - الشركاء");
            WriteHeaders(sheet,
                "Partner / الشريك",
                "Arabic name / الاسم بالعربية",
                "Status / الحالة",
                "Orders / الطلبات",
                "Gross revenue / الإيراد الإجمالي",
                "Net revenue / صافي الإيراد",
                "Delivery fees / رسوم التوصيل",
                "Partner fees / رسوم الشريك",
                "Refunds / المرتجعات",
                "Total commission / إجمالي العمولة",
                "Restaurant share / حصة المطعم",
                "Partner/card share / حصة الشريك أو البطاقة",
                "Net settlement / صافي التسوية",
                "Net after cost sharing / الصافي بعد تقاسم التكلفة",
                "Average order / متوسط الطلب",
                "Cash orders / طلبات نقدية",
                "Cash revenue / إيراد نقدي",
                "Credit orders / طلبات آجلة",
                "Credit revenue / إيراد آجل");

            for (var index = 0; index < rows.Count; index++)
            {
                var row = index + 2;
                sheet.Cell(row, 1).Value = rows[index].Name;
                sheet.Cell(row, 2).Value = rows[index].NameAr ?? rows[index].Name;
                sheet.Cell(row, 3).Value = rows[index].IsActive ? "Active / نشط" : "Inactive / غير نشط";
                sheet.Cell(row, 4).Value = rows[index].OrderCount;
                sheet.Cell(row, 5).Value = rows[index].GrossRevenue;
                sheet.Cell(row, 6).Value = rows[index].NetRevenue;
                sheet.Cell(row, 7).Value = rows[index].DeliveryFees;
                sheet.Cell(row, 8).Value = rows[index].PartnerFees;
                sheet.Cell(row, 9).Value = rows[index].RefundAmount;
                sheet.Cell(row, 10).Value = rows[index].TotalCommission;
                sheet.Cell(row, 11).Value = rows[index].RestaurantShare;
                sheet.Cell(row, 12).Value = rows[index].CounterpartyShare;
                sheet.Cell(row, 13).Value = rows[index].NetSettlement;
                sheet.Cell(row, 14).Value = rows[index].NetRevenueAfterCostSharing;
                sheet.Cell(row, 15).Value = rows[index].AverageOrderValue;
                sheet.Cell(row, 16).Value = rows[index].CashOrderCount;
                sheet.Cell(row, 17).Value = rows[index].CashRevenue;
                sheet.Cell(row, 18).Value = rows[index].CreditOrderCount;
                sheet.Cell(row, 19).Value = rows[index].CreditRevenue;
            }

            sheet.Columns(5, 15).Style.NumberFormat.Format = "#,##0.00";
            sheet.Column(17).Style.NumberFormat.Format = "#,##0.00";
            sheet.Column(19).Style.NumberFormat.Format = "#,##0.00";
            FormatSheet(sheet);
        }

        private static void AddCommissionSheets(
            XLWorkbook workbook,
            CommissionAnalysisDto commission,
            string currency)
        {
            AddCommissionSummarySheet(workbook, commission, currency);
            AddPartnerCommissionSheet(workbook, commission.Partners);
            AddPaymentProviderCommissionSheet(workbook, commission.PaymentProviders);
            AddCommissionOrdersSheet(workbook, commission);
        }

        private static void AddCommissionSummarySheet(
            XLWorkbook workbook,
            CommissionAnalysisDto commission,
            string currency)
        {
            var sheet = workbook.Worksheets.Add("Commission Summary - العمولات");
            WriteHeaders(sheet, "Metric / المؤشر", "Value / القيمة", "Unit / الوحدة");

            var row = 2;
            var s = commission.Summary;
            AddMetric(sheet, ref row, "Total partner commission / إجمالي عمولات الشركاء", s.TotalPartnerCommission, currency);
            AddMetric(sheet, ref row, "Total payment fees / إجمالي رسوم الدفع", s.TotalPaymentFees, currency);
            AddMetric(sheet, ref row, "Restaurant cost sharing / ما يتحمله المطعم", s.TotalRestaurantCostSharing, currency);
            AddMetric(sheet, ref row, "Provider cost sharing / ما يتحمله المزود", s.TotalProviderCostSharing, currency);
            AddMetric(sheet, ref row, "Commission transactions / معاملات العمولة", s.TotalCommissionTransactions);
            AddMetric(sheet, ref row, "Average commission % / متوسط نسبة العمولة", s.AverageCommissionPercentage, "%");
            AddMetric(sheet, ref row, "Average fee per order / متوسط الرسوم لكل طلب", s.AverageFeePerOrder, currency);

            var c = commission.Coverage;
            AddMetric(sheet, ref row, "Analyzed transactions / المعاملات المُحللة", c.Analyzed);
            AddMetric(sheet, ref row, "From saved commission / من العمولة المحفوظة", c.FromSnapshot);
            AddMetric(sheet, ref row, "Reconstructed from history / مُعاد بناؤها", c.Reconstructed);
            AddMetric(sheet, ref row, "Excluded (insufficient data) / مستبعدة", c.Excluded);

            FormatSheet(sheet);
        }

        private static void AddPartnerCommissionSheet(
            XLWorkbook workbook,
            IReadOnlyList<PartnerCommissionDto> rows)
        {
            var sheet = workbook.Worksheets.Add("Partner Commission - الشركاء");
            WriteHeaders(sheet,
                "Partner / الشريك",
                "Arabic name / الاسم بالعربية",
                "Orders / الطلبات",
                "Gross sales / إجمالي المبيعات",
                "Commission % / نسبة العمولة",
                "Commission amount / قيمة العمولة",
                "Restaurant share / حصة المطعم",
                "Partner share / حصة الشريك",
                "Net receivable / إيراد المطعم النهائي",
                "Avg commission/order / متوسط العمولة للطلب",
                "Basis / المصدر");

            for (var index = 0; index < rows.Count; index++)
            {
                var row = index + 2;
                var x = rows[index];
                sheet.Cell(row, 1).Value = x.Partner;
                sheet.Cell(row, 2).Value = x.PartnerAr ?? x.Partner;
                sheet.Cell(row, 3).Value = x.NumberOfOrders;
                sheet.Cell(row, 4).Value = x.GrossSales;
                sheet.Cell(row, 5).Value = x.CommissionPercentage;
                sheet.Cell(row, 6).Value = x.CommissionAmount;
                sheet.Cell(row, 7).Value = x.RestaurantShare;
                sheet.Cell(row, 8).Value = x.PartnerShare;
                sheet.Cell(row, 9).Value = x.RestaurantFinalRevenue;
                sheet.Cell(row, 10).Value = x.AverageCommissionPerOrder;
                sheet.Cell(row, 11).Value = BasisLabel(x.Basis);
            }

            sheet.Columns(4, 10).Style.NumberFormat.Format = "#,##0.00";
            sheet.Column(5).Style.NumberFormat.Format = "0.00";
            FormatSheet(sheet);
        }

        private static void AddPaymentProviderCommissionSheet(
            XLWorkbook workbook,
            IReadOnlyList<PaymentProviderCommissionDto> rows)
        {
            var sheet = workbook.Worksheets.Add("Payment Commission - الدفع");
            WriteHeaders(sheet,
                "Payment method / طريقة الدفع",
                "Arabic name / الاسم بالعربية",
                "Transactions / المعاملات",
                "Gross amount / إجمالي المبلغ",
                "Fee % / نسبة الرسوم",
                "Total fees / قيمة الرسوم",
                "Restaurant share / حصة المطعم",
                "Provider share / حصة المزود",
                "Net received / الصافي المستلم",
                "Basis / المصدر");

            for (var index = 0; index < rows.Count; index++)
            {
                var row = index + 2;
                var x = rows[index];
                sheet.Cell(row, 1).Value = x.PaymentMethod;
                sheet.Cell(row, 2).Value = x.PaymentMethodAr ?? x.PaymentMethod;
                sheet.Cell(row, 3).Value = x.Transactions;
                sheet.Cell(row, 4).Value = x.GrossAmount;
                sheet.Cell(row, 5).Value = x.FeePercentage;
                sheet.Cell(row, 6).Value = x.FeeAmount;
                sheet.Cell(row, 7).Value = x.RestaurantShare;
                sheet.Cell(row, 8).Value = x.ProviderShare;
                sheet.Cell(row, 9).Value = x.NetAmountReceived;
                sheet.Cell(row, 10).Value = BasisLabel(x.Basis);
            }

            sheet.Columns(4, 9).Style.NumberFormat.Format = "#,##0.00";
            sheet.Column(5).Style.NumberFormat.Format = "0.00";
            FormatSheet(sheet);
        }

        private static void AddCommissionOrdersSheet(
            XLWorkbook workbook,
            CommissionAnalysisDto commission)
        {
            var sheet = workbook.Worksheets.Add("Commission Orders - الطلبات");
            WriteHeaders(sheet,
                "Source / المصدر",
                "Order number / رقم الطلب",
                "Date / التاريخ",
                "Customer / العميل",
                "Order total / إجمالي الطلب",
                "Commission % / نسبة العمولة",
                "Commission amount / قيمة العمولة",
                "Restaurant share / حصة المطعم",
                "Provider share / حصة المزود",
                "Final receivable / الصافي",
                "Basis / المصدر");

            var row = 2;
            foreach (var partner in commission.Partners)
                row = WriteCommissionOrders(sheet, row, partner.Partner, partner.Orders);
            foreach (var provider in commission.PaymentProviders)
                row = WriteCommissionOrders(sheet, row, provider.PaymentMethod, provider.Orders);

            sheet.Column(3).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
            sheet.Columns(5, 10).Style.NumberFormat.Format = "#,##0.00";
            sheet.Column(6).Style.NumberFormat.Format = "0.00";
            FormatSheet(sheet);
        }

        private static int WriteCommissionOrders(
            IXLWorksheet sheet,
            int startRow,
            string source,
            IReadOnlyList<CommissionOrderDto> orders)
        {
            for (var index = 0; index < orders.Count; index++)
            {
                var row = startRow + index;
                var o = orders[index];
                sheet.Cell(row, 1).Value = source;
                sheet.Cell(row, 2).Value = o.OrderNumber;
                sheet.Cell(row, 3).Value = o.DateUtc;
                sheet.Cell(row, 4).Value = o.Customer;
                sheet.Cell(row, 5).Value = o.OrderTotal;
                sheet.Cell(row, 6).Value = o.CommissionPercentage;
                sheet.Cell(row, 7).Value = o.CommissionAmount;
                sheet.Cell(row, 8).Value = o.RestaurantShare;
                sheet.Cell(row, 9).Value = o.ProviderShare;
                sheet.Cell(row, 10).Value = o.NetAmount;
                sheet.Cell(row, 11).Value = BasisLabel(o.Basis);
            }

            return startRow + orders.Count;
        }

        private static string BasisLabel(string basis) => basis switch
        {
            CommissionBasis.Reconstructed => "Reconstructed / مُعاد بناؤها",
            CommissionBasis.Mixed => "Mixed / مختلط",
            _ => "Saved / محفوظ"
        };

        private static int WriteRankedRows(
            IXLWorksheet sheet,
            IReadOnlyList<RankedRowDto> rows,
            int startRow,
            string? section)
        {
            for (var index = 0; index < rows.Count; index++)
            {
                var row = startRow + index;
                var offset = section is null ? 0 : 1;
                if (section is not null) sheet.Cell(row, 1).Value = section;
                sheet.Cell(row, 1 + offset).Value = rows[index].Label;
                sheet.Cell(row, 2 + offset).Value = rows[index].LabelAr ?? rows[index].Label;
                sheet.Cell(row, 3 + offset).Value = rows[index].PrimaryValue;
                sheet.Cell(row, 4 + offset).Value = rows[index].Count ?? 0;
            }

            return startRow + rows.Count;
        }

        private static void AddMetric(
            IXLWorksheet sheet,
            ref int row,
            string label,
            object value,
            string? unit = null)
        {
            sheet.Cell(row, 1).Value = label;
            switch (value)
            {
                case DateTime date:
                    sheet.Cell(row, 2).Value = date;
                    sheet.Cell(row, 2).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                    break;
                case decimal number:
                    sheet.Cell(row, 2).Value = number;
                    sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
                    break;
                case int count:
                    sheet.Cell(row, 2).Value = count;
                    break;
                default:
                    sheet.Cell(row, 2).Value = value.ToString() ?? string.Empty;
                    break;
            }
            sheet.Cell(row, 3).Value = unit ?? string.Empty;
            row++;
        }

        private static void WriteHeaders(IXLWorksheet sheet, params string[] headers)
        {
            for (var index = 0; index < headers.Length; index++)
                sheet.Cell(1, index + 1).Value = headers[index];

            var header = sheet.Range(1, 1, 1, headers.Length);
            header.Style.Font.Bold = true;
            header.Style.Font.FontColor = XLColor.White;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F766E");
        }

        private static void FormatSheet(IXLWorksheet sheet)
        {
            sheet.SheetView.FreezeRows(1);
            sheet.RangeUsed()?.SetAutoFilter();
            sheet.Columns().AdjustToContents();
            sheet.Columns().Style.Alignment.WrapText = true;
        }

        private static string NormalizeCurrency(string? currency)
        {
            var normalized = currency?.Trim().ToUpperInvariant();
            return normalized is { Length: 3 } && normalized.All(char.IsLetter)
                ? normalized
                : DefaultCurrency;
        }
    }
}
