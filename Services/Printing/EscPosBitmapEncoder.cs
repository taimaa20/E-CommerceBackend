using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RestaurantPos.Api.Services.Printing
{
    /// <summary>
    /// Converts a PNG (or any ImageSharp-supported format) into raw ESC/POS
    /// bytes that print as a 1-bit raster image on a thermal printer.
    ///
    /// Used by the receipt-image endpoint: the frontend renders the existing
    /// cashier-UI HTML to a PNG, the backend hands the bytes to this encoder,
    /// and the resulting ESC/POS payload is enqueued like any other print
    /// job. The agent prints it as-is — no special handling needed.
    ///
    /// Output format: <c>GS v 0</c> (raster bit image) — the standard
    /// command supported by every modern thermal printer including the
    /// XP-80x family.
    ///
    /// Width is fixed to 576 pixels (80mm paper at 8 dots/mm). PNGs taller
    /// than this scale proportionally; wider PNGs are downscaled. The
    /// 1-bit dithering uses a simple luminance threshold — fast, sharp on
    /// text, acceptable on logos.
    /// </summary>
    public static class EscPosBitmapEncoder
    {
        // 80mm thermal paper at 8 dots/mm = 576 dots. Bytes per row = 72.
        private const int PrinterWidthPx = 576;
        private const int LuminanceThreshold = 128; // 0–255

        /// <summary>
        /// Converts the given image bytes into an ESC/POS payload that
        /// initialises the printer, prints the image as 1-bit raster, then
        /// cuts the paper.
        /// </summary>
        public static byte[] Encode(byte[] imageBytes)
        {
            ArgumentNullException.ThrowIfNull(imageBytes);
            if (imageBytes.Length == 0)
                throw new ArgumentException("Image bytes are empty.", nameof(imageBytes));

            using var img = Image.Load<Rgba32>(imageBytes);

            // Force exact printer width. Height scales proportionally.
            if (img.Width != PrinterWidthPx)
            {
                var ratio = (double)PrinterWidthPx / img.Width;
                var targetHeight = Math.Max(1, (int)Math.Round(img.Height * ratio));
                img.Mutate(ctx => ctx.Resize(PrinterWidthPx, targetHeight));
            }

            var width = img.Width;
            var height = img.Height;
            var bytesPerRow = (width + 7) / 8;
            var raster = new byte[bytesPerRow * height];

            // Threshold to 1-bit. Each output bit: 1 = black (printed), 0 = white.
            img.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    var rowOffset = y * bytesPerRow;
                    for (var x = 0; x < width; x++)
                    {
                        var p = row[x];
                        // ITU-R BT.601 luminance, weighted by alpha — transparent
                        // pixels treat as white so the thermal head doesn't fire
                        // unnecessarily.
                        var alpha = p.A / 255.0;
                        var lum = (0.299 * p.R + 0.587 * p.G + 0.114 * p.B) * alpha
                                  + 255 * (1 - alpha);
                        if (lum < LuminanceThreshold)
                        {
                            raster[rowOffset + (x / 8)] |= (byte)(0x80 >> (x % 8));
                        }
                    }
                }
            });

            return BuildEscPosPayload(raster, bytesPerRow, height);
        }

        private static byte[] BuildEscPosPayload(byte[] raster, int bytesPerRow, int height)
        {
            // Header: ESC @ (init), then GS v 0 m xL xH yL yH <data>.
            // m=0 → normal density. xL/xH = bytes per row. yL/yH = pixel rows.
            var xL = (byte)(bytesPerRow & 0xFF);
            var xH = (byte)((bytesPerRow >> 8) & 0xFF);

            using var ms = new MemoryStream();

            // Initialise printer.
            ms.Write(new byte[] { 0x1B, 0x40 });
            // Center alignment so the image sits in the middle of the strip.
            ms.Write(new byte[] { 0x1B, 0x61, 0x01 });

            // Some printers (Xprinter family) cap raster height at ~256 rows
            // per command. Split into chunks to be safe.
            const int chunkRows = 200;
            for (var rowStart = 0; rowStart < height; rowStart += chunkRows)
            {
                var rows = Math.Min(chunkRows, height - rowStart);
                var yL = (byte)(rows & 0xFF);
                var yH = (byte)((rows >> 8) & 0xFF);

                ms.Write(new byte[] { 0x1D, 0x76, 0x30, 0x00, xL, xH, yL, yH });
                ms.Write(raster, rowStart * bytesPerRow, rows * bytesPerRow);
            }

            // Feed + cut.
            ms.Write(new byte[] { 0x0A, 0x0A, 0x0A }); // 3 line feeds
            ms.Write(new byte[] { 0x1D, 0x56, 0x00 }); // GS V 0 — full cut

            return ms.ToArray();
        }
    }
}
