using ClosedXML.Excel;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Common;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Export
{
    /// <summary>
    /// Shared ClosedXML primitives for the dashboard Excel exports. Centralizes
    /// the bilingual (English / عربي) header style, number/date formats and the
    /// common Category / Ranked / TimeSeries sheet layouts so the Home,
    /// Operations and Inventory export services stay thin and produce a
    /// visually identical workbook to the Financial dashboard reference.
    /// </summary>
    /// <remarks>
    /// Every numeric cell is written from an already-rounded snapshot value, so
    /// the workbook can never surface NaN/Infinity — those are impossible in
    /// .NET decimals and the snapshots guard ratios upstream.
    /// </remarks>
    public static class DashboardWorkbookWriter
    {
        public const string ContentType =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private const string MoneyFormat = "#,##0.00";
        private const string PercentFormat = "0.00";
        private const string HeaderColor = "#0F766E";

        public static DashboardExportFile Build(string fileNamePrefix, Action<XLWorkbook> compose)
        {
            using var workbook = new XLWorkbook();
            compose(workbook);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileName = $"{fileNamePrefix}-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
            return new DashboardExportFile(stream.ToArray(), fileName, ContentType);
        }

        /// <summary>Key/value overview sheet — bilingual labels, typed values, optional unit.</summary>
        public static void OverviewSheet(
            XLWorkbook workbook,
            string sheetName,
            DashboardSnapshotEnvelope envelope,
            IReadOnlyList<(string Label, object? Value, string? Unit)> metrics)
        {
            var sheet = workbook.Worksheets.Add(sheetName);
            WriteHeaders(sheet, "Metric / المؤشر", "Value / القيمة", "Unit / الوحدة");

            var row = 2;
            AddMetric(sheet, ref row, "Period start / بداية الفترة", envelope.WindowStartUtc, null);
            AddMetric(sheet, ref row, "Period end / نهاية الفترة", envelope.WindowEndUtc, null);
            AddMetric(sheet, ref row, "Generated / تاريخ الإنشاء", envelope.GeneratedAtUtc, null);
            foreach (var (label, value, unit) in metrics)
                AddMetric(sheet, ref row, label, value, unit);

            FormatSheet(sheet);
        }

        /// <summary>
        /// Category breakdown sheet (payment split, order-type split, waste-by-category…).
        /// Pass <paramref name="valueIsMoney"/> = false when the value column is a count
        /// (e.g. Home's order-type-by-count) and <paramref name="countHeader"/> = null to
        /// drop the redundant count column.
        /// </summary>
        public static void CategorySheet(
            XLWorkbook workbook,
            string sheetName,
            IReadOnlyList<CategorySliceDto> rows,
            string valueHeader,
            bool valueIsMoney = true,
            string? countHeader = "Count / العدد")
        {
            var sheet = workbook.Worksheets.Add(sheetName);
            var hasCount = countHeader is not null;
            var pctColumn = hasCount ? 5 : 4;

            if (hasCount)
                WriteHeaders(sheet,
                    "Name (English) / الاسم بالإنجليزية",
                    "Name (Arabic) / الاسم بالعربية",
                    valueHeader, countHeader!, "Percentage / النسبة");
            else
                WriteHeaders(sheet,
                    "Name (English) / الاسم بالإنجليزية",
                    "Name (Arabic) / الاسم بالعربية",
                    valueHeader, "Percentage / النسبة");

            for (var index = 0; index < rows.Count; index++)
            {
                var row = index + 2;
                sheet.Cell(row, 1).Value = rows[index].Label;
                sheet.Cell(row, 2).Value = string.IsNullOrWhiteSpace(rows[index].LabelAr) ? rows[index].Label : rows[index].LabelAr;
                sheet.Cell(row, 3).Value = rows[index].Value;
                if (hasCount) sheet.Cell(row, 4).Value = rows[index].Count ?? 0;
                sheet.Cell(row, pctColumn).Value = rows[index].Percentage ?? 0m;
            }

            sheet.Column(3).Style.NumberFormat.Format = valueIsMoney ? MoneyFormat : "#,##0";
            sheet.Column(pctColumn).Style.NumberFormat.Format = PercentFormat;
            FormatSheet(sheet);
        }

        /// <summary>Ranked rows sheet (top products, top cashiers, waste by employee…).</summary>
        public static void RankedSheet(
            XLWorkbook workbook,
            string sheetName,
            IReadOnlyList<RankedRowDto> rows,
            string valueHeader,
            string? secondaryHeader,
            string countHeader = "Count / العدد")
        {
            var sheet = workbook.Worksheets.Add(sheetName);
            var hasSecondary = secondaryHeader is not null;
            if (hasSecondary)
                WriteHeaders(sheet,
                    "Name (English) / الاسم بالإنجليزية",
                    "Name (Arabic) / الاسم بالعربية",
                    valueHeader, secondaryHeader!, countHeader);
            else
                WriteHeaders(sheet,
                    "Name (English) / الاسم بالإنجليزية",
                    "Name (Arabic) / الاسم بالعربية",
                    valueHeader, countHeader);

            for (var index = 0; index < rows.Count; index++)
            {
                var row = index + 2;
                sheet.Cell(row, 1).Value = rows[index].Label;
                sheet.Cell(row, 2).Value = string.IsNullOrWhiteSpace(rows[index].LabelAr) ? rows[index].Label : rows[index].LabelAr;
                sheet.Cell(row, 3).Value = rows[index].PrimaryValue;
                if (hasSecondary)
                {
                    sheet.Cell(row, 4).Value = rows[index].SecondaryValue ?? 0m;
                    sheet.Cell(row, 5).Value = rows[index].Count ?? 0;
                }
                else
                {
                    sheet.Cell(row, 4).Value = rows[index].Count ?? 0;
                }
            }

            sheet.Column(3).Style.NumberFormat.Format = MoneyFormat;
            if (hasSecondary) sheet.Column(4).Style.NumberFormat.Format = MoneyFormat;
            FormatSheet(sheet);
        }

        /// <summary>Time-series sheet (revenue trend, hourly orders, waste daily trend).</summary>
        public static void TimeSeriesSheet(
            XLWorkbook workbook,
            string sheetName,
            string dateHeader,
            string valueHeader,
            IReadOnlyList<TimeSeriesPointDto> rows,
            bool dateOnly)
        {
            var sheet = workbook.Worksheets.Add(sheetName);
            WriteHeaders(sheet, dateHeader, valueHeader, "Count / العدد");

            for (var index = 0; index < rows.Count; index++)
            {
                var row = index + 2;
                sheet.Cell(row, 1).Value = rows[index].BucketStartUtc;
                sheet.Cell(row, 2).Value = rows[index].Value;
                sheet.Cell(row, 3).Value = rows[index].Count ?? 0;
            }

            sheet.Column(1).Style.DateFormat.Format = dateOnly ? "yyyy-mm-dd" : "yyyy-mm-dd hh:mm";
            sheet.Column(2).Style.NumberFormat.Format = MoneyFormat;
            FormatSheet(sheet);
        }

        private static void AddMetric(IXLWorksheet sheet, ref int row, string label, object? value, string? unit)
        {
            sheet.Cell(row, 1).Value = label;
            switch (value)
            {
                case null:
                    sheet.Cell(row, 2).Value = string.Empty;
                    break;
                case DateTime date:
                    sheet.Cell(row, 2).Value = date;
                    sheet.Cell(row, 2).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                    break;
                case decimal number:
                    sheet.Cell(row, 2).Value = number;
                    sheet.Cell(row, 2).Style.NumberFormat.Format = MoneyFormat;
                    break;
                case double dbl:
                    sheet.Cell(row, 2).Value = dbl;
                    sheet.Cell(row, 2).Style.NumberFormat.Format = PercentFormat;
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
            header.Style.Fill.BackgroundColor = XLColor.FromHtml(HeaderColor);
        }

        private static void FormatSheet(IXLWorksheet sheet)
        {
            sheet.SheetView.FreezeRows(1);
            sheet.RangeUsed()?.SetAutoFilter();
            sheet.Columns().AdjustToContents();
            sheet.Columns().Style.Alignment.WrapText = true;
        }
    }

    public sealed record DashboardExportFile(byte[] Content, string FileName, string ContentType);
}
