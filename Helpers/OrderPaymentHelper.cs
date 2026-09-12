using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Helpers
{
    public static class OrderPaymentHelper
    {
        public const string PendingStatus = "Pending";
        public const string PartialStatus = "Partial";
        public const string PaidStatus = "Paid";
        public const string FailedStatus = "Failed";
        public const string RefundedStatus = "Refunded";

        public static OrderPaymentSnapshot BuildSnapshot(Order order)
        {
            ArgumentNullException.ThrowIfNull(order);

            var payments = (order.Payments ?? Enumerable.Empty<Payment>())
                .OrderBy(p => p.CreatedAt)
                .ToList();

            var paymentDtos = payments
                .Select(MapPayment)
                .ToList();

            var totalAmount = RoundCurrency(order.TotalAmount);
            decimal paidAmount;
            string paymentStatus;

            if (paymentDtos.Count > 0)
            {
                paidAmount = RoundCurrency(paymentDtos.Sum(p => p.Amount));
                var remainingAmount = RoundCurrency(Math.Max(0m, totalAmount - paidAmount));
                paymentStatus = IsPaid(order) ||
                    (order.Status != OrderStatus.Cancelled && remainingAmount <= 0)
                    ? PaidStatus
                    : paidAmount <= 0
                    ? PendingStatus
                    : PartialStatus;

                return new OrderPaymentSnapshot
                {
                    PaidAmount = paidAmount,
                    RemainingAmount = remainingAmount,
                    PaymentStatus = paymentStatus,
                    IsPaid = paymentStatus == PaidStatus,
                    PaymentMethod = paymentStatus == PaidStatus
                        ? BuildPaymentMethodSummary(paymentDtos.Select(p => p.Method)) ?? order.PaymentMethod
                        : order.PaymentMethod,
                    AmountTendered = order.AmountTendered,
                    ChangeAmount = order.ChangeAmount,
                    Payments = paymentDtos
                };
            }

            if (IsPaid(order))
            {
                paidAmount = totalAmount;
                paymentStatus = PaidStatus;
            }
            else
            {
                paidAmount = 0m;
                paymentStatus = PendingStatus;
            }

            return new OrderPaymentSnapshot
            {
                PaidAmount = paidAmount,
                RemainingAmount = RoundCurrency(Math.Max(0m, totalAmount - paidAmount)),
                PaymentStatus = paymentStatus,
                IsPaid = paymentStatus == PaidStatus,
                PaymentMethod = order.PaymentMethod,
                AmountTendered = order.AmountTendered,
                ChangeAmount = order.ChangeAmount,
                Payments = paymentDtos
            };
        }

        public static PaymentDto MapPayment(Payment payment)
        {
            ArgumentNullException.ThrowIfNull(payment);

            return new PaymentDto
            {
                Id = payment.Id,
                Amount = RoundCurrency(payment.Amount),
                Method = payment.Method,
                PaymentMethodId = payment.PaymentMethodId,
                PaymentMethodName = payment.PaymentMethodName,
                PaymentMethodNameAr = payment.PaymentMethodNameAr,
                PaymentMethodCode = payment.PaymentMethodCode,
                ReferenceNumber = payment.ReferenceNumber,
                CostSharingMode = payment.CostSharingMode,
                CostSharingScope = payment.CostSharingScope,
                CostSharingCommissionPercentage = payment.CostSharingCommissionPercentage,
                CostSharingRestaurantPercentage = payment.CostSharingRestaurantPercentage,
                CostSharingCounterpartyPercentage = payment.CostSharingCounterpartyPercentage,
                CostSharingCommissionAmount = payment.CostSharingCommissionAmount,
                CostSharingRestaurantShareAmount = payment.CostSharingRestaurantShareAmount,
                CostSharingCounterpartyShareAmount = payment.CostSharingCounterpartyShareAmount,
                CreatedAt = payment.CreatedAt
            };
        }

        public static string ResolvePaymentDisplayName(Payment payment)
        {
            ArgumentNullException.ThrowIfNull(payment);

            if (!IsCashMethod(payment.Method) && !string.IsNullOrWhiteSpace(payment.PaymentMethodName))
                return payment.PaymentMethodName.Trim();

            return string.IsNullOrWhiteSpace(payment.Method) ? "Cash" : payment.Method.Trim();
        }

        public static string? BuildPaymentDisplaySummary(IEnumerable<Payment> payments)
        {
            var uniqueMethods = payments
                .Select(ResolvePaymentDisplayName)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return uniqueMethods.Count == 0
                ? null
                : string.Join(" + ", uniqueMethods);
        }

        public static string? BuildPaymentMethodSummary(IEnumerable<string?> methods)
        {
            var uniqueMethods = methods
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return uniqueMethods.Count == 0
                ? null
                : string.Join(" + ", uniqueMethods);
        }

        public static bool IsCashMethod(string? paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod))
                return true;

            var normalized = paymentMethod.Trim().ToLowerInvariant();
            return normalized.Contains("cash") || normalized.Contains("nakit");
        }

        // Card-scheme tokens, matched against a configured method's code or either display
        // name. Kept beside IsCashMethod so payment-method classification has one home.
        private static readonly string[] CardMethodTokens =
            ["card", "visa", "master", "meeza", "credit", "debit", "بطاق"];

        /// <summary>
        /// True when a configured payment method is a card scheme. Unlike
        /// <see cref="IsCashMethod"/> an unknown/blank method is NOT a card, and a method that
        /// already reads as cash (e.g. "Vodafone Cash") is never reclassified here.
        /// </summary>
        public static bool IsCardMethod(string? paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod) || IsCashMethod(paymentMethod))
                return false;

            var normalized = paymentMethod.Trim().ToLowerInvariant();
            return CardMethodTokens.Any(token => normalized.Contains(token, StringComparison.Ordinal));
        }

        // ─── Legacy-data label normalization ────────────────────────────────
        // Payments created before the configurable PaymentMethods registry was
        // attached only have the free-text `Method` snapshot (e.g. "Credit Card",
        // "Card", "Online"). For analytics we collapse all non-cash legacy
        // variants into a single "Card (legacy)" bucket so dashboards don't
        // surface a misleading provider name.
        public const string LegacyCardKey      = "LEGACY_CARD";
        public const string LegacyCardLabelEn  = "Card (legacy)";
        public const string LegacyCardLabelAr  = "بطاقة (سابقة)";
        public const string CashKey            = "CASH";
        public const string CashLabelEn        = "Cash";
        public const string CashLabelAr        = "نقدي";

        /// <summary>
        /// Bucket a payment whose configurable-method snapshot is missing.
        /// Returns a stable (Key, LabelEn, LabelAr) so multiple legacy variants
        /// of the same kind fold into one slice.
        /// </summary>
        public static (string Key, string Label, string LabelAr) ResolveLegacyBucket(string? legacyMethod)
        {
            if (IsCashMethod(legacyMethod))
                return (CashKey, CashLabelEn, CashLabelAr);

            return (LegacyCardKey, LegacyCardLabelEn, LegacyCardLabelAr);
        }

        public static decimal RoundCurrency(decimal amount)
            => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        public static bool IsPaid(Order order)
        {
            ArgumentNullException.ThrowIfNull(order);

            if (order.OrderSource == OrderSource.Online)
            {
                var recordedAmount = (order.Payments ?? Enumerable.Empty<Payment>()).Sum(payment => payment.Amount);
                return order.Status != OrderStatus.Cancelled &&
                    (order.PaidAt.HasValue ||
                     order.Status is OrderStatus.Paid or OrderStatus.Completed ||
                     recordedAmount >= order.TotalAmount);
            }

            return order.Status != OrderStatus.Cancelled &&
                (order.PaidAt.HasValue ||
                 order.Status == OrderStatus.Paid ||
                 order.Status == OrderStatus.Completed ||
                 !string.IsNullOrWhiteSpace(order.PaymentMethod));
        }
    }

    public sealed class OrderPaymentSnapshot
    {
        public decimal PaidAmount { get; init; }
        public decimal RemainingAmount { get; init; }
        public string PaymentStatus { get; init; } = OrderPaymentHelper.PendingStatus;
        public bool IsPaid { get; init; }
        public string? PaymentMethod { get; init; }
        public decimal? AmountTendered { get; init; }
        public decimal? ChangeAmount { get; init; }
        public List<PaymentDto> Payments { get; init; } = new();
    }
}
