using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;
using System.Globalization;
using System.Security.Cryptography;

namespace RestaurantPos.Api.Services
{
    public interface IPublicReceiptService
    {
        Task<PublicReceiptShareDto> CreateWhatsAppShareAsync(
            Guid orderId,
            Guid tenantId,
            bool isArabic,
            string publicBaseUrl,
            CancellationToken ct);

        Task<PublicReceiptDto> GetReceiptAsync(string token, bool isArabic, CancellationToken ct);

        Task<string> GetOrCreateAccessTokenAsync(
            Guid orderId,
            Guid tenantId,
            CancellationToken ct);
    }

    public sealed class PublicReceiptService : IPublicReceiptService
    {
        private const int TokenByteLength = 32;
        private const int TokenMaxAttempts = 3;
        private const int PublicReceiptTokenLifetimeDays = 90;
        private const int MinInternationalPhoneLength = 8;
        private const int MaxInternationalPhoneLength = 15;
        private const int TokenMaxLength = 96;
        private const string DefaultBusinessNameEn = "Sandobox";
        private const string DefaultBusinessNameAr = "ساندوبوكس";
        private const string DefaultSettingsName = "RestoPOS";
        private const string DefaultCurrency = "JOD";

        private readonly IPublicReceiptRepository _repository;

        public PublicReceiptService(IPublicReceiptRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<PublicReceiptShareDto> CreateWhatsAppShareAsync(
            Guid orderId,
            Guid tenantId,
            bool isArabic,
            string publicBaseUrl,
            CancellationToken ct)
        {
            ValidateCreateRequest(orderId, tenantId, publicBaseUrl);

            var nowUtc = DateTime.UtcNow;
            var seed = await _repository.GetOrderSeedAsync(orderId, tenantId, ct)
                ?? throw new NotFoundException("Order", orderId);
            var customerPhone = NormalizeWhatsAppPhone(seed.CustomerPhone);
            var token = await GetOrCreateTokenAsync(seed, nowUtc, ct);
            var receiptUrl = BuildReceiptUrl(publicBaseUrl, token.Token);
            var receipt = await _repository.GetReceiptAsync(token.Token, isArabic, nowUtc, ct)
                ?? throw new NotFoundException("Receipt link was not found or is no longer active.");

            return new PublicReceiptShareDto
            {
                ReceiptUrl = receiptUrl,
                CustomerPhone = customerPhone,
                Message = BuildWhatsAppMessage(receipt, receiptUrl, isArabic),
                ExpiresAtUtc = token.ExpireAtUtc
            };
        }

        public async Task<PublicReceiptDto> GetReceiptAsync(string token, bool isArabic, CancellationToken ct)
        {
            var normalizedToken = NormalizeToken(token);
            return await _repository.GetReceiptAsync(normalizedToken, isArabic, DateTime.UtcNow, ct)
                ?? throw new NotFoundException("Receipt link was not found or is no longer active.");
        }

        public async Task<string> GetOrCreateAccessTokenAsync(
            Guid orderId,
            Guid tenantId,
            CancellationToken ct)
        {
            ValidateOrderIdentity(orderId, tenantId);
            var seed = await _repository.GetOrderSeedAsync(orderId, tenantId, ct)
                ?? throw new NotFoundException("Order", orderId);
            var token = await GetOrCreateTokenAsync(seed, DateTime.UtcNow, ct);
            return token.Token;
        }

        private async Task<PublicReceiptToken> GetOrCreateTokenAsync(
            PublicReceiptOrderSeed seed,
            DateTime nowUtc,
            CancellationToken ct)
        {
            var existing = await _repository.GetActiveTokenForOrderAsync(seed.OrderId, seed.TenantId, nowUtc, ct);
            if (existing != null)
                return existing;

            var token = new PublicReceiptToken
            {
                Id = Guid.NewGuid(),
                TenantId = seed.TenantId,
                OrderId = seed.OrderId,
                Token = await GenerateUniqueTokenAsync(ct),
                CreatedAtUtc = nowUtc,
                ExpireAtUtc = nowUtc.AddDays(PublicReceiptTokenLifetimeDays),
                IsActive = true
            };

            await _repository.AddTokenAsync(token, ct);
            await _repository.SaveChangesAsync(ct);
            return token;
        }

        private async Task<string> GenerateUniqueTokenAsync(CancellationToken ct)
        {
            for (var attempt = 0; attempt < TokenMaxAttempts; attempt++)
            {
                var token = GenerateToken();
                if (!await _repository.TokenExistsAsync(token, ct))
                    return token;
            }

            throw new ConflictException("Could not create a unique receipt token.");
        }

        private static string BuildWhatsAppMessage(PublicReceiptDto receipt, string receiptUrl, bool isArabic)
        {
            var businessName = ResolveMessageBusinessName(receipt.Restaurant.Name, isArabic);
            var currency = string.IsNullOrWhiteSpace(receipt.Restaurant.Currency)
                ? DefaultCurrency
                : receipt.Restaurant.Currency.Trim();
            var orderType = receipt.OrderType == OrderType.Delivery.ToString()
                ? (isArabic ? "توصيل" : "Delivery")
                : receipt.OrderType == OrderType.Takeaway.ToString()
                    ? (isArabic ? "سفري" : "Takeaway")
                    : (isArabic ? "محلي" : "Dine-in");
            var deliveryFee = receipt.DeliveryFee ?? 0m;
            var deliveryArea = string.IsNullOrWhiteSpace(receipt.DeliveryZoneName)
                ? null
                : receipt.DeliveryZoneName.Trim();

            var lines = isArabic
                ? new List<string>
                {
                    $"شكراً لطلبك من {businessName}",
                    "",
                    $"رقم الطلب: {receipt.OrderNumber}",
                    $"نوع الطلب: {orderType}"
                }
                : new List<string>
                {
                    $"Thank you for your order from {businessName}.",
                    "",
                    $"Order No: {receipt.OrderNumber}",
                    $"Order Type: {orderType}"
                };

            if (receipt.OrderType == OrderType.Delivery.ToString() && deliveryArea != null)
            {
                lines.Add(isArabic
                    ? $"منطقة التوصيل: {deliveryArea}"
                    : $"Delivery Area: {deliveryArea}");
            }

            if (deliveryFee > 0)
            {
                lines.Add(isArabic
                    ? $"رسوم التوصيل: {FormatMoney(deliveryFee, currency)}"
                    : $"Delivery Fee: {FormatMoney(deliveryFee, currency)}");
            }

            lines.Add(isArabic
                ? $"الإجمالي: {FormatMoney(receipt.TotalAmount, currency)}"
                : $"Total: {FormatMoney(receipt.TotalAmount, currency)}");

            if (receipt.PaidAmount > 0)
            {
                lines.Add(isArabic
                    ? $"المدفوع: {FormatMoney(receipt.PaidAmount, currency)}"
                    : $"Paid: {FormatMoney(receipt.PaidAmount, currency)}");
            }

            if (receipt.RemainingAmount > 0)
            {
                lines.Add(isArabic
                    ? $"المتبقي: {FormatMoney(receipt.RemainingAmount, currency)}"
                    : $"Due: {FormatMoney(receipt.RemainingAmount, currency)}");
            }

            lines.Add("");
            lines.Add(isArabic ? "الفاتورة:" : "Receipt:");
            lines.Add(receiptUrl);

            return string.Join("\n", lines);
        }

        private static string ResolveMessageBusinessName(string? configuredName, bool isArabic)
        {
            if (string.IsNullOrWhiteSpace(configuredName) ||
                configuredName.Equals(DefaultSettingsName, StringComparison.OrdinalIgnoreCase))
            {
                return isArabic ? DefaultBusinessNameAr : DefaultBusinessNameEn;
            }

            return configuredName.Trim();
        }

        private static string FormatMoney(decimal amount, string currency)
        {
            return $"{currency} {amount.ToString("0.00", CultureInfo.InvariantCulture)}";
        }

        private static string BuildReceiptUrl(string publicBaseUrl, string token)
        {
            var baseUri = new Uri(EnsureTrailingSlash(publicBaseUrl), UriKind.Absolute);
            return new Uri(baseUri, $"receipt/public/{Uri.EscapeDataString(token)}").ToString();
        }

        private static string EnsureTrailingSlash(string value)
        {
            return value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";
        }

        private static string GenerateToken()
        {
            Span<byte> bytes = stackalloc byte[TokenByteLength];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string NormalizeWhatsAppPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                throw new ValidationException("Customer phone missing");

            var trimmed = NormalizeArabicDigits(phone).Trim();
            var hasExplicitCountryCode = trimmed.StartsWith("+", StringComparison.Ordinal);
            var digits = new string(trimmed.Where(char.IsDigit).ToArray());

            if (digits.StartsWith("00", StringComparison.Ordinal))
            {
                digits = digits[2..];
                hasExplicitCountryCode = true;
            }

            if (hasExplicitCountryCode)
                return ValidateInternationalPhone(digits);

            if (digits.StartsWith("20", StringComparison.Ordinal) ||
                digits.StartsWith("962", StringComparison.Ordinal))
            {
                return ValidateInternationalPhone(digits);
            }

            var local = digits.TrimStart('0');
            if (local.StartsWith("1", StringComparison.Ordinal) && local.Length == 10)
                return ValidateInternationalPhone($"20{local}");

            if (local.StartsWith("7", StringComparison.Ordinal) && local.Length == 9)
                return ValidateInternationalPhone($"962{local}");

            return ValidateInternationalPhone(local);
        }

        private static string ValidateInternationalPhone(string digits)
        {
            if (digits.Length is < MinInternationalPhoneLength or > MaxInternationalPhoneLength)
                throw new ValidationException("Invalid customer phone");

            return digits;
        }

        private static string NormalizeArabicDigits(string value)
        {
            return string.Concat(value.Select(ch => ch switch
            {
                >= '٠' and <= '٩' => (char)('0' + ch - '٠'),
                >= '۰' and <= '۹' => (char)('0' + ch - '۰'),
                _ => ch
            }));
        }

        private static string NormalizeToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ValidationException("Receipt token is required.");

            var normalized = token.Trim();
            return normalized.Length <= TokenMaxLength
                ? normalized
                : throw new ValidationException("Receipt token is invalid.");
        }

        private static void ValidateCreateRequest(Guid orderId, Guid tenantId, string publicBaseUrl)
        {
            ValidateOrderIdentity(orderId, tenantId);

            if (!Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var uri) ||
                (!uri.IsLoopback && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ValidationException("Receipt base URL must be HTTPS.");
            }
        }

        private static void ValidateOrderIdentity(Guid orderId, Guid tenantId)
        {
            if (orderId == Guid.Empty)
                throw new ValidationException("Order is required.");
            if (tenantId == Guid.Empty)
                throw new ValidationException("Tenant is required.");
        }
    }
}
