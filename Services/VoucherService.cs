using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services
{
    public sealed class VoucherService : IVoucherService
    {
        private readonly IVoucherRepository _repository;
        private readonly ISettingsService _settingsService;

        public VoucherService(
            IVoucherRepository repository,
            ISettingsService settingsService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        public async Task<VoucherAvailabilityDto> GetAvailabilityAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await _settingsService.GetSettingsAsync(cancellationToken: ct);
            var businessDate = GetBusinessDate();
            var usedToday = await _repository.CountUsedTodayAsync(tenantId, businessDate, ct);
            return BuildAvailability(settings, businessDate, usedToday);
        }

        public async Task<VoucherAuditSummaryDto> GetTodayAuditAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await _settingsService.GetSettingsAsync(cancellationToken: ct);
            var businessDate = GetBusinessDate();
            var orders = await _repository.GetTodayAuditAsync(tenantId, businessDate, ct);
            var availability = BuildAvailability(settings, businessDate, orders.Count);

            return new VoucherAuditSummaryDto
            {
                Enabled = availability.Enabled,
                VoucherAmount = availability.VoucherAmount,
                DailyLimit = availability.DailyLimit,
                UsedToday = availability.UsedToday,
                RemainingToday = availability.RemainingToday,
                BusinessDate = availability.BusinessDate,
                Orders = orders
            };
        }

        public async Task<VoucherApplicationResult> ApplyToOrderAsync(
            Order order,
            Guid? userId,
            decimal currentTotal,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(order);
            var settings = await _settingsService.GetSettingsAsync(cancellationToken: ct);
            ValidateSettings(settings);
            ValidateOrder(order);

            var businessDate = GetBusinessDate();
            await _repository.AcquireDailyUsageLockAsync(order.TenantId, businessDate, ct);

            if (await _repository.ExistsForOrderAsync(order.TenantId, order.Id, ct))
                throw new InvalidOperationException("A voucher has already been applied to this order.");

            var usedToday = await _repository.CountUsedTodayAsync(order.TenantId, businessDate, ct);
            if (usedToday >= settings.VoucherDailyLimit)
                throw new InvalidOperationException("Daily voucher limit has been reached.");

            var now = DateTime.UtcNow;
            var total = OrderPaymentHelper.RoundCurrency(currentTotal);
            var discount = OrderPaymentHelper.RoundCurrency(Math.Min(settings.VoucherAmount, total));
            var finalTotal = OrderPaymentHelper.RoundCurrency(Math.Max(0m, total - discount));

            order.IsVoucherApplied = true;
            order.VoucherDiscountAmount = discount;
            order.VoucherAppliedAt = now;
            order.TotalAmount = finalTotal;

            var audit = new VoucherUsageAudit
            {
                Id = Guid.NewGuid(),
                TenantId = order.TenantId,
                OrderId = order.Id,
                BusinessDate = businessDate,
                UsedAt = now,
                DiscountAmount = discount,
                VoucherCode = VoucherDefaults.Code,
                CreatedByUserId = userId
            };
            await _repository.InsertAuditAsync(audit, ct);

            return new VoucherApplicationResult
            {
                DiscountAmount = discount,
                FinalTotal = finalTotal,
                RemainingToday = Math.Max(0, settings.VoucherDailyLimit - usedToday - 1),
                Audit = audit
            };
        }

        private static VoucherAvailabilityDto BuildAvailability(SystemSettings settings, DateOnly businessDate, int usedToday)
        {
            var enabled = settings.VoucherEnabled && settings.VoucherAmount > 0 && settings.VoucherDailyLimit > 0;
            var remaining = enabled ? Math.Max(0, settings.VoucherDailyLimit - usedToday) : 0;

            return new VoucherAvailabilityDto
            {
                Enabled = enabled,
                VoucherAmount = OrderPaymentHelper.RoundCurrency(settings.VoucherAmount),
                DailyLimit = settings.VoucherDailyLimit,
                UsedToday = usedToday,
                RemainingToday = remaining,
                BusinessDate = businessDate
            };
        }

        private static void ValidateSettings(SystemSettings settings)
        {
            if (!settings.VoucherEnabled)
                throw new InvalidOperationException("Voucher is disabled.");

            if (settings.VoucherAmount <= 0)
                throw new InvalidOperationException("Voucher amount must be greater than zero.");

            if (settings.VoucherDailyLimit <= 0)
                throw new InvalidOperationException("Daily voucher limit must be greater than zero.");
        }

        private static void ValidateOrder(Order order)
        {
            if (order.IsVoucherApplied)
                throw new InvalidOperationException("A voucher has already been applied to this order.");

            if (order.Payments?.Any() == true)
                throw new InvalidOperationException("Voucher cannot be applied after payment has started.");

            if (order.Status == OrderStatus.Cancelled ||
                order.Status == OrderStatus.Paid ||
                order.Status == OrderStatus.Completed)
                throw new InvalidOperationException("Voucher cannot be applied to this order status.");

            if (order.TotalAmount <= 0)
                throw new InvalidOperationException("Voucher cannot be applied to a zero-total order.");
        }

        private static DateOnly GetBusinessDate()
            => DateOnly.FromDateTime(DateTime.UtcNow);
    }
}
