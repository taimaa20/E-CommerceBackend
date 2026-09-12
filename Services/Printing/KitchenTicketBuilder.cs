using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using System.Net;
using System.Text;

namespace RestaurantPos.Api.Services.Printing
{
    public class KitchenTicketBuilder : IKitchenTicketBuilder
    {
        // 28-char wrap fits the condensed (ESC SI) font on 80mm paper without
        // overflow, leaves a small right margin, and keeps Arabic glyphs from
        // colliding with the cutter edge.
        private const int KitchenLineWidth = 28;
        private const string KitchenTitleFallback = "Kitchen";
        private const string ItemContinuationPrefix = "   ";
        private const string DetailPrefix = "   ";
        private const string DetailContinuationPrefix = "     ";
        private const string OptionMarker = "~";
        private const string ExtraMarker = "+";
        private const string AlternativeMarker = ">";
        private const string NoteMarker = "!";
        private const string ReviewQrLabel = "Scan to Review";
        private const string MenuQrLabel = "Scan for Menu";

        public byte[] BuildKitchenTicket(Order order, Kitchen kitchen, IReadOnlyList<OrderItem> items, int codePage, bool isReprint = false)
        {
            ArgumentNullException.ThrowIfNull(order);
            ArgumentNullException.ThrowIfNull(kitchen);
            ArgumentNullException.ThrowIfNull(items);

            var b = new EscPosBuilder(codePage, KitchenLineWidth);
            b.FontNormal().SmallFont();
            AppendKitchenHeader(b, order, kitchen);
            AppendReprintLabel(b, isReprint);
            AppendKitchenDetails(b, order, DateTime.UtcNow.ToLocalTime());
            AppendKitchenItems(b, items);
            b.Separator('=');

            // Feed + cut are appended once per copy by FinalizeKitchenTicketCopy.
            return b.Build();
        }

        private static void AppendKitchenHeader(EscPosBuilder b, Order order, Kitchen kitchen)
        {
            var kitchenName = string.IsNullOrWhiteSpace(kitchen.Name)
                ? KitchenTitleFallback
                : kitchen.Name.Trim();

            b.Center().Bold(true)
                .WrappedLine(kitchenName, ItemContinuationPrefix)
                .Normal().Bold(false);

            AppendCenteredArabicLine(b, kitchen.NameAr, kitchenName);
            b.Center().Line($"** {ResolveKitchenSourceBadge(order)} **").Left();
            b.Separator('=');
        }

        private static void AppendReprintLabel(EscPosBuilder b, bool isReprint)
        {
            if (!isReprint) return;
            b.Center().Bold(true).Line("*** REPRINT ***").Bold(false).Left();
            b.Separator('=');
        }

        private static void AppendKitchenDetails(EscPosBuilder b, Order order, DateTime printedAt)
        {
            b.Bold(true);
            b.RowWrapped("Order:", ResolveDisplayOrderNumber(order));
            if (order.OrderSource != OrderSource.Pos)
                b.RowWrapped("Source:", ResolveKitchenSourceBadge(order));
            if (order.OrderSource == OrderSource.DeliveryPartner && !string.IsNullOrWhiteSpace(order.PartnerOrderNumber))
                b.RowWrapped("Partner #:", order.PartnerOrderNumber);
            if (!string.IsNullOrWhiteSpace(order.TableName) && order.OrderType != OrderType.Takeaway)
                b.RowWrapped("Table:", order.TableName);
            if (order.TicketId > 0)
                b.RowWrapped("Ticket:", order.TicketId.ToString());
            b.RowWrapped("Date:", printedAt.ToString("yyyy-MM-dd"));
            b.RowWrapped("Time:", printedAt.ToString("HH:mm:ss"));
            b.Bold(false).Separator('-');
        }

        private static void AppendKitchenItems(EscPosBuilder b, IReadOnlyList<OrderItem> items)
        {
            var uniqueItems = items.DistinctBy(item => item.Id).ToList();

            for (var i = 0; i < uniqueItems.Count; i++)
            {
                AppendKitchenItem(b, uniqueItems[i]);
                if (i < uniqueItems.Count - 1) b.Separator('-');
            }
        }

        private static void AppendKitchenItem(EscPosBuilder b, OrderItem item)
        {
            b.Bold(true)
                .WrappedLine($"{item.Quantity}x {item.ProductName}", ItemContinuationPrefix)
                .Normal().Bold(false);

            AppendArabicLine(b, WithItemQuantity(item.Product?.NameAr, item.Quantity), DetailPrefix, item.ProductName);
            AppendOptionalPair(b, OptionMarker, item.SelectedOptionName, item.SelectedOptionNameAr);
            AppendKitchenModifiers(b, item);
            AppendKitchenAlternatives(b, item);
            AppendOptionalLine(b, NoteMarker, item.Notes);
            if (item.IsNewlyAdded) b.Bold(true).Line($"{DetailPrefix}*** NEW ***").Bold(false);
        }

        private static void AppendKitchenModifiers(EscPosBuilder b, OrderItem item)
        {
            if (item.Modifiers == null) return;

            foreach (var mod in item.Modifiers)
            {
                var qtyPrefix = mod.Quantity > 1 ? $"{mod.Quantity}x " : string.Empty;
                b.WrappedLine($"{DetailPrefix}{ExtraMarker} {qtyPrefix}{mod.ModifierName}", DetailContinuationPrefix);
                AppendArabicLine(b, mod.ModifierNameAr, DetailContinuationPrefix, mod.ModifierName);
            }
        }

        private static void AppendKitchenAlternatives(EscPosBuilder b, OrderItem item)
        {
            if (item.RecipeSnapshotItems == null) return;

            foreach (var alternative in item.RecipeSnapshotItems.Where(IsSelectedAlternative))
            {
                var name = ResolveAlternativeName(alternative);
                var nameAr = ResolveAlternativeNameAr(alternative);
                AppendOptionalPair(b, AlternativeMarker, name, nameAr);
            }
        }

        private static bool IsSelectedAlternative(OrderItemRecipeSnapshot snapshot)
            => snapshot.SourceAlternativeId.HasValue;

        private static string ResolveKitchenSourceBadge(Order order)
        {
            if (order.OrderSource == OrderSource.DeliveryPartner)
                return ResolvePartnerDisplayName(order).ToUpperInvariant();

            return order.OrderSource == OrderSource.Talabat
                ? "TALABAT"
                : order.OrderType.ToString().ToUpperInvariant();
        }

        private static string ResolvePartnerDisplayName(Order order)
            => !string.IsNullOrWhiteSpace(order.DeliveryPartnerName)
                ? order.DeliveryPartnerName
                : !string.IsNullOrWhiteSpace(order.DeliveryPartnerCode)
                    ? order.DeliveryPartnerCode
                    : "DELIVERY PARTNER";

        private static string? ResolveAlternativeName(OrderItemRecipeSnapshot snapshot)
            => string.IsNullOrWhiteSpace(snapshot.SourceAlternativeName)
                ? snapshot.RawMaterialName
                : snapshot.SourceAlternativeName;

        private static string? ResolveAlternativeNameAr(OrderItemRecipeSnapshot snapshot)
            => string.IsNullOrWhiteSpace(snapshot.SourceAlternativeNameAr)
                ? snapshot.RawMaterialNameAr
                : snapshot.SourceAlternativeNameAr;

        private static void AppendOptionalPair(EscPosBuilder b, string marker, string? name, string? nameAr)
        {
            AppendOptionalLine(b, marker, name);
            AppendArabicLine(b, nameAr, DetailContinuationPrefix, name);
        }

        private static void AppendOptionalLine(EscPosBuilder b, string marker, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            b.WrappedLine($"{DetailPrefix}{marker} {value.Trim()}", DetailContinuationPrefix);
        }

        private static void AppendArabicLine(EscPosBuilder b, string? value, string prefix, string? compareTo)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var text = value.Trim();
            if (string.Equals(text, compareTo?.Trim(), StringComparison.OrdinalIgnoreCase)) return;
            b.WrappedLine(prefix + text, prefix);
        }

        private static void AppendCenteredArabicLine(EscPosBuilder b, string? value, string compareTo)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var text = value.Trim();
            if (string.Equals(text, compareTo.Trim(), StringComparison.OrdinalIgnoreCase)) return;
            b.Center().WrappedLine(text, ItemContinuationPrefix).Left();
        }

        public string BuildKitchenTicketHtml(Order order, Kitchen kitchen, IReadOnlyList<OrderItem> items, bool isReprint = false)
        {
            ArgumentNullException.ThrowIfNull(order);
            ArgumentNullException.ThrowIfNull(kitchen);
            ArgumentNullException.ThrowIfNull(items);

            var printedAt = DateTime.UtcNow.ToLocalTime();
            var sb = new StringBuilder();
            AppendHtmlStart(sb);
            AppendHtmlHeader(sb, order, kitchen);
            AppendHtmlReprintLabel(sb, isReprint);
            AppendHtmlDetails(sb, order, printedAt);
            AppendHtmlItems(sb, items);
            sb.Append("<div class=\"sep\"></div></div></body></html>");
            return sb.ToString();
        }

        public byte[] BuildCustomerReceipt(Order order, IReadOnlyList<OrderItem> items, int codePage, SystemSettings? settings)
        {
            ArgumentNullException.ThrowIfNull(order);
            ArgumentNullException.ThrowIfNull(items);

            var b = new EscPosBuilder(codePage);
            var currency = settings?.Currency ?? "JOD";

            // ── Header: business name (large), address, phone, tax number ──
            var businessName = string.IsNullOrWhiteSpace(settings?.RestaurantName)
                ? "Restaurant"
                : settings!.RestaurantName;

            b.Center().Big().Bold(true).Line(businessName).Normal().Bold(false);

            if (!string.IsNullOrWhiteSpace(settings?.RestaurantAddress))
                b.Center().Line(settings!.RestaurantAddress!);
            if (!string.IsNullOrWhiteSpace(settings?.RestaurantPhone))
                b.Center().Line($"Tel: {settings!.RestaurantPhone}");
            if (!string.IsNullOrWhiteSpace(settings?.TaxNumber))
                b.Center().Line($"Tax No: {settings!.TaxNumber}");

            b.Left().Separator('=');

            // ── Order block ──
            b.Center().TallOnly().Bold(true).Line("RECEIPT").Normal().Bold(false);
            b.Center().Line($"Order #{ResolveDisplayOrderNumber(order)}");
            b.Left().Separator('-');

            b.Row("Type:", order.OrderType.ToString());
            if (order.OrderSource != OrderSource.Pos)
                b.Row("Source:", order.OrderSource == OrderSource.DeliveryPartner ? "Delivery Partner" : order.OrderSource.ToString());
            if (ShouldPrintDeliveryAddress(order))
                b.Row("Address:", order.DeliveryAddress!);
            if (order.OrderSource == OrderSource.DeliveryPartner)
            {
                b.Row("Partner:", ResolvePartnerDisplayName(order));
                if (!string.IsNullOrWhiteSpace(order.PartnerOrderNumber))
                    b.Row("Partner #:", order.PartnerOrderNumber!);
                if (!string.IsNullOrWhiteSpace(order.PartnerCustomerName))
                    b.Row("Customer:", order.PartnerCustomerName!);
                if (!string.IsNullOrWhiteSpace(order.PartnerCustomerPhone))
                    b.Row("Phone:", order.PartnerCustomerPhone!);
            }
            if (!string.IsNullOrWhiteSpace(order.TableName))
                b.Row("Table:", order.TableName);
            if (order.TicketId > 0)
                b.Row("Ticket:", order.TicketId.ToString());
            if (!string.IsNullOrWhiteSpace(order.CustomerPhone))
                b.Row("Customer:", order.CustomerPhone!);
            b.Row("Time:", order.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
            b.Separator('-');

            // ── Items + modifiers ──
            foreach (var item in items)
            {
                var lineTotal = item.IsComplimentary ? 0m : item.Price * item.Quantity;
                b.Row($"{item.Quantity}x {item.ProductName}", lineTotal.ToString("0.00"));

                if (!string.IsNullOrWhiteSpace(item.SelectedOptionName))
                    b.Line($"   ~ {item.SelectedOptionName}");

                if (item.Modifiers != null)
                {
                    foreach (var mod in item.Modifiers)
                    {
                        var modPrice = mod.Price * Math.Max(1, mod.Quantity);
                        var qtyPrefix = mod.Quantity > 1 ? $"{mod.Quantity}x " : "";
                        b.Row($"   + {qtyPrefix}{mod.ModifierName}",
                              modPrice == 0 ? "" : modPrice.ToString("0.00"));
                    }
                }

                if (!string.IsNullOrWhiteSpace(item.Notes))
                    b.Line($"   ! {item.Notes}");

                if (item.IsComplimentary)
                    b.Line("   * Complimentary");
            }

            // ── Totals ──
            b.Separator('-');
            b.Row("Subtotal", order.Subtotal.ToString("0.00"));
            if (order.DiscountAmount > 0)
                b.Row($"Discount {order.DiscountPercentage:0.##}%", $"-{order.DiscountAmount:0.00}");
            if (order.DiscountGroupAmount > 0)
            {
                var groupLabel = order.DiscountGroupType == DiscountValueType.Percentage
                    ? $"{order.DiscountGroupName ?? "Group"} {order.DiscountGroupValue:0.##}%"
                    : $"{order.DiscountGroupName ?? "Group"}";
                b.Row(groupLabel, $"-{order.DiscountGroupAmount:0.00}");
            }
            if (order.ServiceChargeAmount > 0)
                b.Row($"Service {order.ServiceChargeRate:0.##}%", order.ServiceChargeAmount.ToString("0.00"));
            if ((settings?.ShowTaxOnReceipt ?? true) && order.TaxAmount > 0)
                b.Row($"Tax {order.TaxRate:0.##}%", order.TaxAmount.ToString("0.00"));
            if ((order.DeliveryFee ?? 0m) > 0)
                b.Row("Delivery Fee", order.DeliveryFee.Value.ToString("0.00"));
            if ((order.PartnerDeliveryFee ?? 0m) > 0)
                b.Row("Partner Delivery", order.PartnerDeliveryFee.Value.ToString("0.00"));
            if ((order.PartnerServiceFee ?? 0m) > 0)
                b.Row("Partner Service", order.PartnerServiceFee.Value.ToString("0.00"));

            b.Separator('=');
            b.Bold(true).Big().Row("TOTAL", $"{order.TotalAmount:0.00} {currency}").Normal().Bold(false);

            // ── Payment details (if any) ──
            if (!string.IsNullOrWhiteSpace(order.PaymentMethod))
            {
                var paymentMethodDisplay = OrderPaymentHelper.BuildPaymentDisplaySummary(order.Payments);
                b.Separator('-');
                b.Row("Payment Type:", order.PaymentMethod!);
                if (!string.IsNullOrWhiteSpace(paymentMethodDisplay) &&
                    !string.Equals(paymentMethodDisplay, order.PaymentMethod, StringComparison.OrdinalIgnoreCase))
                    b.Row("Payment Method:", paymentMethodDisplay);
                var paymentReferenceDisplay = BuildPaymentReferenceSummary(order.Payments);
                if (!string.IsNullOrWhiteSpace(paymentReferenceDisplay))
                    b.Row("Reference:", paymentReferenceDisplay);
                if (order.AmountTendered.HasValue && order.AmountTendered.Value > 0)
                    b.Row("Tendered:", order.AmountTendered.Value.ToString("0.00"));
                if (order.ChangeAmount.HasValue && order.ChangeAmount.Value > 0)
                    b.Row("Change:", order.ChangeAmount.Value.ToString("0.00"));
            }

            // ── Footer note ──
            if (!string.IsNullOrWhiteSpace(settings?.ReceiptFooterNote))
            {
                b.Separator('-').Center().Line(settings!.ReceiptFooterNote!).Left();
            }

            var receiptQr = GetReceiptQr(settings);
            if (receiptQr is not null)
            {
                b.LF(1).Center().Line(receiptQr.Value.Label).QrCode(receiptQr.Value.Url, moduleSize: 4).Left();
            }

            b.LF(3).Cut();

            return b.Build();
        }

        private static bool ShouldPrintDeliveryAddress(Order order)
        {
            return !string.IsNullOrWhiteSpace(order.DeliveryAddress)
                && (order.OrderType == OrderType.Delivery
                    || order.OrderSource == OrderSource.Talabat
                    || order.OrderSource == OrderSource.DeliveryPartner);
        }

        private static ReceiptQr? GetReceiptQr(SystemSettings? settings)
        {
            if (!string.IsNullOrWhiteSpace(settings?.GoogleReviewUrl))
            {
                return new ReceiptQr(settings.GoogleReviewUrl.Trim(), ReviewQrLabel);
            }

            if (!string.IsNullOrWhiteSpace(settings?.QrMenuUrl))
            {
                return new ReceiptQr(settings.QrMenuUrl.Trim(), MenuQrLabel);
            }

            return null;
        }

        private readonly record struct ReceiptQr(string Url, string Label);

        private static void AppendHtmlStart(StringBuilder sb)
        {
            sb.Append("<!doctype html><html><head><meta charset=\"utf-8\"><style>");
            sb.Append("*{box-sizing:border-box}body{margin:0;width:80mm;background:#fff;color:#000;font-family:Tahoma,Arial,sans-serif;font-size:12px}");
            sb.Append(".ticket{padding:6px 8px 0;overflow:hidden}.center{text-align:center}.title{font-size:22px;font-weight:700;line-height:1.1;overflow-wrap:anywhere}");
            sb.Append(".mode{font-size:13px;font-weight:700}.sep{border-top:2px solid #000;margin:6px 0}.dash{border-top:1px dashed #000;margin:6px 0}");
            sb.Append(".row{display:flex;gap:8px;justify-content:space-between;align-items:flex-start}.label{font-weight:700}.value{flex:1;text-align:right;unicode-bidi:plaintext;overflow-wrap:anywhere}");
            sb.Append(".item{font-size:17px;font-weight:700;margin:7px 0 3px;unicode-bidi:plaintext;overflow-wrap:anywhere}.item-ar{font-size:16px;font-weight:700;margin:2px 0 3px;unicode-bidi:plaintext;overflow-wrap:anywhere}");
            sb.Append(".sub,.sub-ar,.new{font-size:13px;margin:2px 0 0 14px;unicode-bidi:plaintext;overflow-wrap:anywhere}.new{font-weight:700}.reprint{font-size:15px;font-weight:700;text-align:center;margin:6px 0}[dir=rtl]{direction:rtl;text-align:right}");
            sb.Append("</style></head><body><div class=\"ticket\">");
        }

        private static void AppendHtmlHeader(StringBuilder sb, Order order, Kitchen kitchen)
        {
            HtmlLine(sb, "center title", kitchen.Name);
            HtmlOptionalDistinctLine(sb, "center", kitchen.NameAr, string.Empty, kitchen.Name);
            HtmlLine(sb, "center mode", $"** {ResolveKitchenSourceBadge(order)} **");
            sb.Append("<div class=\"sep\"></div>");
        }

        private static void AppendHtmlReprintLabel(StringBuilder sb, bool isReprint)
        {
            if (!isReprint) return;
            HtmlLine(sb, "reprint", "*** REPRINT ***");
            sb.Append("<div class=\"sep\"></div>");
        }

        private static void AppendHtmlDetails(StringBuilder sb, Order order, DateTime printedAt)
        {
            HtmlRow(sb, "Order:", ResolveDisplayOrderNumber(order));
            if (order.OrderSource != OrderSource.Pos)
                HtmlRow(sb, "Source:", ResolveKitchenSourceBadge(order));
            if (order.OrderSource == OrderSource.DeliveryPartner && !string.IsNullOrWhiteSpace(order.PartnerOrderNumber))
                HtmlRow(sb, "Partner #:", order.PartnerOrderNumber);
            if (!string.IsNullOrWhiteSpace(order.TableName) && order.OrderType != OrderType.Takeaway)
                HtmlRow(sb, "Table:", order.TableName);
            if (order.TicketId > 0)
                HtmlRow(sb, "Ticket:", order.TicketId.ToString());
            HtmlRow(sb, "Date:", printedAt.ToString("yyyy-MM-dd"));
            HtmlRow(sb, "Time:", printedAt.ToString("HH:mm:ss"));
            sb.Append("<div class=\"dash\"></div>");
        }

        private static void AppendHtmlItems(StringBuilder sb, IReadOnlyList<OrderItem> items)
        {
            foreach (var item in items)
            {
                HtmlLine(sb, "item", $"{item.Quantity}x {item.ProductName}");
                HtmlOptionalDistinctLine(sb, "item-ar", WithItemQuantity(item.Product?.NameAr, item.Quantity), string.Empty, item.ProductName);
                HtmlOptionalLine(sb, "sub", item.SelectedOptionName, OptionMarker + " ");
                HtmlOptionalDistinctLine(sb, "sub-ar", item.SelectedOptionNameAr, string.Empty, item.SelectedOptionName);
                AppendModifiers(sb, item);
                AppendAlternatives(sb, item);
                HtmlOptionalLine(sb, "sub", item.Notes, NoteMarker + " ");
                if (item.IsNewlyAdded) HtmlLine(sb, "new", "*** NEW ***");
            }
        }

        private static void AppendModifiers(StringBuilder sb, OrderItem item)
        {
            if (item.Modifiers == null) return;

            foreach (var mod in item.Modifiers)
            {
                var qtyPrefix = mod.Quantity > 1 ? $"{mod.Quantity}x " : string.Empty;
                HtmlLine(sb, "sub", $"{ExtraMarker} {qtyPrefix}{mod.ModifierName}");
                HtmlOptionalDistinctLine(sb, "sub-ar", mod.ModifierNameAr, string.Empty, mod.ModifierName);
            }
        }

        private static void AppendAlternatives(StringBuilder sb, OrderItem item)
        {
            if (item.RecipeSnapshotItems == null) return;

            foreach (var alternative in item.RecipeSnapshotItems.Where(IsSelectedAlternative))
            {
                var name = ResolveAlternativeName(alternative);
                var nameAr = ResolveAlternativeNameAr(alternative);
                HtmlOptionalLine(sb, "sub", name, AlternativeMarker + " ");
                HtmlOptionalDistinctLine(sb, "sub-ar", nameAr, string.Empty, name);
            }
        }

        private static void HtmlOptionalLine(StringBuilder sb, string cssClass, string? value, string prefix)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            HtmlLine(sb, cssClass, prefix + value.Trim());
        }

        private static string BuildPaymentReferenceSummary(IEnumerable<Payment>? payments)
        {
            return string.Join(" + ", (payments ?? Enumerable.Empty<Payment>())
                .Select(payment => payment.ReferenceNumber?.Trim())
                .Where(reference => !string.IsNullOrWhiteSpace(reference))
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static void HtmlOptionalDistinctLine(
            StringBuilder sb,
            string cssClass,
            string? value,
            string prefix,
            string? compareTo)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var text = value.Trim();
            if (string.Equals(text, compareTo?.Trim(), StringComparison.OrdinalIgnoreCase)) return;
            HtmlLine(sb, cssClass, prefix + text);
        }

        private static string? WithItemQuantity(string? value, int quantity)
            => string.IsNullOrWhiteSpace(value)
                ? value
                : $"{value.Trim()} x {quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        private static void HtmlLine(StringBuilder sb, string cssClass, string value)
        {
            var dir = EscPosFormatter.ContainsArabic(value) ? "rtl" : "ltr";
            sb.Append("<div class=\"").Append(cssClass).Append("\" dir=\"").Append(dir).Append("\">")
                .Append(WebUtility.HtmlEncode(value))
                .Append("</div>");
        }

        private static void HtmlRow(StringBuilder sb, string label, string value)
        {
            var dir = EscPosFormatter.ContainsArabic(value) ? "rtl" : "ltr";
            sb.Append("<div class=\"row\"><span class=\"label\">")
                .Append(WebUtility.HtmlEncode(label))
                .Append("</span><span class=\"value\" dir=\"")
                .Append(dir)
                .Append("\">")
                .Append(WebUtility.HtmlEncode(value))
                .Append("</span></div>");
        }

        // Receipts and kitchen tickets must show the same identifier the cashier
        // sees on the order card. Mirrors OrdersController.ResolveOrderNumber.
        private static string ResolveDisplayOrderNumber(Order order)
        {
            if (!string.IsNullOrWhiteSpace(order.DisplayOrderNumber))
                return order.DisplayOrderNumber!;
            if (!string.IsNullOrWhiteSpace(order.PublicOrderNumber))
                return order.PublicOrderNumber!;
            return order.OrderNumber ?? string.Empty;
        }
    }
}
