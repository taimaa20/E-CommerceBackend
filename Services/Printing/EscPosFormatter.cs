using System.Text;

namespace RestaurantPos.Api.Services.Printing
{
    /// <summary>
    /// Minimal ESC/POS byte builder for thermal receipt printers (Epson TM-T20/T88
    /// family and compatible clones). Only the commands actually used by
    /// kitchen-ticket and receipt builders are wrapped.
    /// </summary>
    public static class EscPosFormatter
    {
        public const int DefaultCodePage = 0;
        public const int Windows1252CodePage = 16;
        // Existing admin UI value for CP864; ESC/POS selects that table with 25.
        public const int LegacyArabicCodePage = 19;
        public const int ArabicPc864CodePage = 25;
        public const int ArabicWindows1256CodePage = 34;
        private const int DotNetPc437CodePage = 437;
        private const int DotNetPc864CodePage = 864;
        private const int DotNetWindows1252CodePage = 1252;
        private const int DotNetWindows1256CodePage = 1256;

        // Initialize printer
        public static readonly byte[] Init = { 0x1B, 0x40 };

        // Line feed
        public static readonly byte[] Lf = { 0x0A };

        public static byte[] FeedLines(byte count) => new byte[] { 0x1B, 0x64, count };

        // Cut paper (full cut)
        public static readonly byte[] CutFull = { 0x1D, 0x56, 0x00 };
        public static byte[] CutFullAfterFeed(byte feedAmount) => new byte[] { 0x1D, 0x56, 0x42, feedAmount };

        // Tail appended after each kitchen-ticket copy.
        //
        // Six LFs clear the print-head-to-cutter gap on 80mm thermal printers
        // without adding the long blank tail that made kitchen tickets wasteful.
        //
        //   0x0A x6         — feed before cut.
        //   0x1D 0x56 0x00   — GS V 0: full cut at current paper position.
        public static readonly byte[] KitchenCopyCut =
        {
            0x0A, 0x0A, 0x0A,
            0x0A, 0x0A, 0x0A,
            0x1D, 0x56, 0x00
        };

        // ESC M 0 — select Font A (normal). Explicit reset so a previous
        // job left in Font B doesn't bleed into the next ticket.
        public static readonly byte[] FontNormal = { 0x1B, 0x4D, 0x00 };

        // Cash drawer kick (pin 2) — harmless for kitchen printers
        public static readonly byte[] DrawerKick = { 0x1B, 0x70, 0x00, 0x19, 0xFA };

        // Alignment
        public static readonly byte[] AlignLeft = { 0x1B, 0x61, 0x00 };
        public static readonly byte[] AlignCenter = { 0x1B, 0x61, 0x01 };
        public static readonly byte[] AlignRight = { 0x1B, 0x61, 0x02 };

        // Text size: { width, height } combined into 0..7 each
        public static readonly byte[] SizeNormal = { 0x1D, 0x21, 0x00 };
        public static readonly byte[] SizeDoubleHeight = { 0x1D, 0x21, 0x01 };
        public static readonly byte[] SizeDoubleWidth = { 0x1D, 0x21, 0x10 };
        public static readonly byte[] SizeDoubleBoth = { 0x1D, 0x21, 0x11 };

        // Bold on/off
        public static readonly byte[] BoldOn = { 0x1B, 0x45, 0x01 };
        public static readonly byte[] BoldOff = { 0x1B, 0x45, 0x00 };

        // Underline
        public static readonly byte[] UnderlineOn = { 0x1B, 0x2D, 0x01 };
        public static readonly byte[] UnderlineOff = { 0x1B, 0x2D, 0x00 };

        public static byte[] SelectCodePage(int codePage)
            => new byte[] { 0x1B, 0x74, (byte)ResolvePrinterCodePage(codePage) };

        /// <summary>
        /// Encode text with the printer's selected code page. Falls back to
        /// ASCII for characters the page can't represent (CP437 default).
        /// </summary>
        public static byte[] EncodeText(string text, int codePage)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<byte>();
            var encoding = ResolveEncoding(codePage);
            return encoding.GetBytes(text);
        }

        public static byte[] FinalizeKitchenTicketCopy(byte[] payload)
        {
            ArgumentNullException.ThrowIfNull(payload);
            var finalized = new byte[payload.Length + KitchenCopyCut.Length];
            Buffer.BlockCopy(payload, 0, finalized, 0, payload.Length);
            Buffer.BlockCopy(KitchenCopyCut, 0, finalized, payload.Length, KitchenCopyCut.Length);
            return finalized;
        }

        public static bool ContainsArabic(string? text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.Any(c => c is >= '\u0600' and <= '\u06FF'
                or >= '\u0750' and <= '\u077F'
                or >= '\u08A0' and <= '\u08FF'
                or >= '\uFB50' and <= '\uFDFF'
                or >= '\uFE70' and <= '\uFEFF');
        }

        public static int UseArabicCodePageIfNeeded(int codePage, bool hasArabic)
            => !hasArabic ? codePage : IsArabicCodePage(codePage) ? codePage : ArabicWindows1256CodePage;

        public static bool IsArabicCodePage(int codePage)
            => codePage is ArabicWindows1256CodePage or ArabicPc864CodePage or LegacyArabicCodePage;

        private static int ResolvePrinterCodePage(int codePage)
        {
            return codePage switch
            {
                LegacyArabicCodePage or ArabicPc864CodePage => ArabicPc864CodePage,
                ArabicWindows1256CodePage => ArabicWindows1256CodePage,
                Windows1252CodePage => Windows1252CodePage,
                _ => DefaultCodePage
            };
        }

        private static int ResolveDotNetCodePage(int codePage)
        {
            return codePage switch
            {
                LegacyArabicCodePage or ArabicPc864CodePage => DotNetPc864CodePage,
                ArabicWindows1256CodePage => DotNetWindows1256CodePage,
                Windows1252CodePage => DotNetWindows1252CodePage,
                _ => DotNetPc437CodePage
            };
        }

        private static Encoding ResolveEncoding(int codePage)
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                return Encoding.GetEncoding(
                    ResolveDotNetCodePage(codePage),
                    new EncoderReplacementFallback("?"),
                    new DecoderReplacementFallback("?"));
            }
            catch
            {
                return Encoding.ASCII;
            }
        }
    }

    /// <summary>
    /// Fluent byte buffer for building an ESC/POS payload. Not thread-safe;
    /// instantiate per ticket.
    /// </summary>
    public sealed class EscPosBuilder
    {
        private const int DefaultLineWidth = 42; // 80mm paper, font A
        private readonly List<byte> _buf = new(capacity: 1024);
        private readonly int _codePage;
        private readonly int _lineWidth;

        public EscPosBuilder(int codePage = 0, int lineWidth = DefaultLineWidth)
        {
            _codePage = codePage;
            _lineWidth = lineWidth > 0 ? lineWidth : DefaultLineWidth;
            _buf.AddRange(EscPosFormatter.Init);
            _buf.AddRange(EscPosFormatter.SelectCodePage(codePage));
            _buf.AddRange(EscPosFormatter.AlignLeft);
        }

        public EscPosBuilder Raw(byte[] bytes) { _buf.AddRange(bytes); return this; }
        public EscPosBuilder Text(string s) { _buf.AddRange(EscPosFormatter.EncodeText(s, _codePage)); return this; }
        public EscPosBuilder Line(string s = "") { Text(s); _buf.AddRange(EscPosFormatter.Lf); return this; }
        public EscPosBuilder LF(int count = 1) { for (var i = 0; i < count; i++) _buf.AddRange(EscPosFormatter.Lf); return this; }
        public EscPosBuilder FeedLines(byte count) { _buf.AddRange(EscPosFormatter.FeedLines(count)); return this; }
        public EscPosBuilder Center() { _buf.AddRange(EscPosFormatter.AlignCenter); return this; }
        public EscPosBuilder Left() { _buf.AddRange(EscPosFormatter.AlignLeft); return this; }
        public EscPosBuilder Right() { _buf.AddRange(EscPosFormatter.AlignRight); return this; }
        public EscPosBuilder Bold(bool on) { _buf.AddRange(on ? EscPosFormatter.BoldOn : EscPosFormatter.BoldOff); return this; }
        public EscPosBuilder Big() { _buf.AddRange(EscPosFormatter.SizeDoubleBoth); return this; }
        public EscPosBuilder TallOnly() { _buf.AddRange(EscPosFormatter.SizeDoubleHeight); return this; }
        public EscPosBuilder Normal() { _buf.AddRange(EscPosFormatter.SizeNormal); return this; }
        public EscPosBuilder FontNormal() { _buf.AddRange(EscPosFormatter.FontNormal); return this; }

        // Kitchen-only compact mode.
        //   ESC ! 0 (0x1B 0x21 0x00) — reset all attributes (clean Font A).
        //   SI      (0x0F)          — engage condensed mode. Note: SI is a
        //                             standalone control byte, NOT preceded
        //                             by ESC. The original spec had `0x1B 0x0F`
        //                             but `ESC SI` is an undefined sequence;
        //                             on Xprinter firmware the ESC consumes
        //                             the next byte without effect, leaving
        //                             condensed mode disabled and confusing
        //                             the parser downstream.
        public EscPosBuilder SmallFont()
        {
            _buf.AddRange(new byte[] { 0x1B, 0x21, 0x00, 0x0F });
            return this;
        }

        public EscPosBuilder Separator(char ch = '-')
        {
            return Line(new string(ch, _lineWidth));
        }

        public EscPosBuilder WrappedLine(string text, string continuationPrefix = "")
        {
            foreach (var line in WrapText(text, _lineWidth, continuationPrefix))
            {
                Line(line);
            }

            return this;
        }

        public EscPosBuilder RowWrapped(string left, string right)
        {
            left ??= string.Empty;
            right ??= string.Empty;

            var rightWidth = Math.Max(1, _lineWidth - left.Length - 1);
            var lines = WrapText(right, rightWidth, string.Empty);
            Row(left, lines[0]);

            for (var i = 1; i < lines.Count; i++)
            {
                Row(string.Empty, lines[i]);
            }

            return this;
        }

        /// <summary>
        /// Two-column row: left text + right text padded to line width.
        /// </summary>
        public EscPosBuilder Row(string left, string right)
        {
            left ??= string.Empty;
            right ??= string.Empty;
            var space = _lineWidth - left.Length - right.Length;
            if (space < 1)
            {
                // Fallback when the text overflows: print on two lines.
                Line(left);
                Right();
                Line(right);
                Left();
                return this;
            }
            return Line(left + new string(' ', space) + right);
        }

        public EscPosBuilder Cut() { _buf.AddRange(EscPosFormatter.CutFull); return this; }
        public EscPosBuilder CutAfterFeed(byte feedAmount) { _buf.AddRange(EscPosFormatter.CutFullAfterFeed(feedAmount)); return this; }

        private static List<string> WrapText(string text, int width, string continuationPrefix)
        {
            var lines = new List<string>();
            var safeWidth = Math.Max(1, width);
            foreach (var paragraph in NormalizeLines(text).Split('\n'))
            {
                WrapParagraph(paragraph, safeWidth, continuationPrefix ?? string.Empty, lines);
            }

            return lines.Count == 0 ? new List<string> { string.Empty } : lines;
        }

        private static void WrapParagraph(string paragraph, int width, string continuationPrefix, List<string> lines)
        {
            if (string.IsNullOrWhiteSpace(paragraph))
            {
                lines.Add(string.Empty);
                return;
            }

            var remaining = paragraph.Trim();
            var prefix = string.Empty;
            while (remaining.Length > 0)
            {
                var take = TakeLineLength(remaining, Math.Max(1, width - prefix.Length));
                lines.Add(prefix + remaining[..take].TrimEnd());
                remaining = remaining[take..].TrimStart();
                prefix = continuationPrefix;
            }
        }

        private static int TakeLineLength(string text, int maxLength)
        {
            if (text.Length <= maxLength) return text.Length;

            for (var i = maxLength; i > 0; i--)
            {
                if (char.IsWhiteSpace(text[i - 1])) return i - 1;
            }

            return maxLength;
        }

        private static string NormalizeLines(string? text)
            => (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');

        /// <summary>
        /// Native ESC/POS QR code. Supported by all modern thermal printers
        /// (Epson, Xprinter XP-80x, Bixolon, etc.) via the
        /// <c>GS ( k</c> family of commands. Renders far sharper than a
        /// bitmap and uses a fraction of the bytes.
        /// </summary>
        /// <param name="data">Text to encode (URL, plain text, etc.).</param>
        /// <param name="moduleSize">Cell size 1..16. 6 is a good default for an 80mm receipt.</param>
        /// <param name="errorCorrection">L=48, M=49 (default), Q=50, H=51.</param>
        public EscPosBuilder QrCode(string data, byte moduleSize = 6, byte errorCorrection = 49)
        {
            if (string.IsNullOrEmpty(data)) return this;

            // 1. Model: GS ( k pL pH cn fn n1 n2 — set model 2
            _buf.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00 });

            // 2. Module size: GS ( k pL pH cn fn n
            _buf.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, moduleSize });

            // 3. Error correction
            _buf.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, errorCorrection });

            // 4. Store data
            var bytes = System.Text.Encoding.UTF8.GetBytes(data);
            var len = bytes.Length + 3;
            var pL = (byte)(len & 0xFF);
            var pH = (byte)((len >> 8) & 0xFF);
            _buf.AddRange(new byte[] { 0x1D, 0x28, 0x6B, pL, pH, 0x31, 0x50, 0x30 });
            _buf.AddRange(bytes);

            // 5. Print
            _buf.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30 });

            return this;
        }

        public byte[] Build() => _buf.ToArray();
    }
}
