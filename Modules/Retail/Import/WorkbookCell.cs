using ClosedXML.Excel;

namespace RestaurantPos.Api.Modules.Retail.Import
{
    /// <summary>
    /// The only place ClosedXML is touched. Reads cached values for formula cells so the
    /// workbook is never recalculated (its VLOOKUP/COUNTIF chains would fail or, worse,
    /// silently produce different numbers than the file the business actually uses).
    /// </summary>
    internal static class WorkbookCell
    {
        private const char NonBreakingSpace = ' ';
        private const string ZeroWidthSpace = "​";

        public static string? Text(IXLWorksheet sheet, int row, int column)
        {
            var value = Read(sheet.Cell(row, column));
            if (value.IsBlank || value.IsError)
                return null;

            var text = value.IsNumber
                ? FormatNumber(value.GetNumber())
                : value.ToString();

            text = Clean(text);
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        public static decimal? Number(IXLWorksheet sheet, int row, int column)
        {
            var value = Read(sheet.Cell(row, column));
            if (value.IsBlank || value.IsError)
                return null;
            if (value.IsNumber)
                return (decimal)value.GetNumber();

            var text = Clean(value.ToString());
            return decimal.TryParse(text, out var parsed) ? parsed : null;
        }

        public static DateTime? Date(IXLWorksheet sheet, int row, int column)
        {
            var value = Read(sheet.Cell(row, column));
            if (value.IsBlank || value.IsError)
                return null;
            if (value.IsDateTime)
                return DateTime.SpecifyKind(value.GetDateTime().Date, DateTimeKind.Utc);
            if (value.IsNumber)
                return DateTime.SpecifyKind(DateTime.FromOADate(value.GetNumber()).Date, DateTimeKind.Utc);

            return DateTime.TryParse(Clean(value.ToString()), out var parsed)
                ? DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc)
                : null;
        }

        private static XLCellValue Read(IXLCell cell) => cell.HasFormula ? cell.CachedValue : cell.Value;

        /// <summary>Integral values must not import as "6.29036059142E+12" or "1.0".</summary>
        private static string FormatNumber(double number)
            => number == Math.Floor(number) && Math.Abs(number) < 1e15
                ? ((long)number).ToString()
                : number.ToString("0.####");

        /// <summary>The workbook carries non-breaking and zero-width spaces inside phone
        /// numbers and e-mail addresses; plain Trim() does not remove them.</summary>
        private static string Clean(string? text)
            => (text ?? string.Empty)
                .Replace(NonBreakingSpace, ' ')
                .Replace(ZeroWidthSpace, string.Empty)
                .Trim();
    }
}
