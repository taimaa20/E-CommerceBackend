using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services
{
    /// <inheritdoc cref="IShiftCloseValidator"/>
    public class ShiftCloseValidator : IShiftCloseValidator
    {
        private readonly IShiftRulesConfigService _configService;
        private readonly ICashierShiftRepository _repository;

        public ShiftCloseValidator(
            IShiftRulesConfigService configService,
            ICashierShiftRepository repository)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _repository    = repository    ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<ShiftCloseValidationDto> ValidateAsync(
            Guid tenantId,
            CashierBalanceShift shift,
            DateTime windowEndUtc,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(shift);

            var config = await _configService.GetForTenantAsync(tenantId, ct);

            // No config OR explicit override → always allowed.
            // Force-allowed reflects the toggle so the UI can decide whether to
            // expose the force-close button.
            if (config is null || config.AllowShiftCloseWithOpenOrders)
            {
                return new ShiftCloseValidationDto
                {
                    IsAllowed = true,
                    ForceCloseAllowed = config?.AllowForcedShiftClose ?? false,
                };
            }

            var counts = await _repository.GetShiftValidationCountsAsync(
                tenantId, shift.BranchId, shift.CashierId, shift.OpenedAt, windowEndUtc, ct);

            var blockers = new List<ShiftCloseBlockerDto>();

            void AddIfAny(string code, int count)
            {
                if (count > 0) blockers.Add(new ShiftCloseBlockerDto { Code = code, Count = count });
            }

            // Hard "RequireAll" gates — any qualifying order blocks close.
            // Counts come from CashierShiftRepository.GetShiftValidationCountsAsync
            // and use PaidAt (not Status==Paid) for the financial-settlement
            // semantic, matching the cashier's mental model from the UI badges.
            if (config.RequireAllOrdersPaid)      AddIfAny("PendingPayment", counts.UnpaidCount);
            if (config.RequireAllOrdersReady)     AddIfAny("NotReady",       counts.NotReadyCount);
            if (config.RequireAllOrdersServed)    AddIfAny("NotServed",      counts.NotServedCount);
            if (config.RequireAllOrdersCompleted) AddIfAny("NotCompleted",   counts.NotCompletedCount);

            // Permissive "Allow" gates — when false, that status blocks close.
            // NOTE: AllowPendingDeliveryOrders is intentionally NOT honored anymore.
            // Order completion is unified across OrderTypes (see Helpers/OrderCompletion.cs)
            // so Delivery / Partner / Talabat / Careem orders share the same rules as
            // DineIn and Takeaway — no per-channel gate.
            if (!config.AllowPendingOrders)               AddIfAny("Pending",              counts.NewCount);
            if (!config.AllowPreparingOrders)             AddIfAny("Preparing",            counts.PreparingCount);
            if (!config.AllowReadyOrders)                 AddIfAny("Ready",                counts.ReadyCount);
            if (!config.AllowPendingCancellationRequests) AddIfAny("PendingCancellation",  counts.PendingCancellationCount);

            return new ShiftCloseValidationDto
            {
                IsAllowed         = blockers.Count == 0,
                Blockers          = blockers,
                ForceCloseAllowed = config.AllowForcedShiftClose,
            };
        }
    }
}
