using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.Services
{
    /// <summary>
    /// Pure, deterministic earning-rule evaluation. Rules are stackable; each qualifying rule
    /// yields its own award (traceable to its RuleVersionId). The tier multiplier is applied last.
    /// No DB or clock dependency beyond the passed-in <paramref name="nowUtc"/> — fully unit-testable.
    /// </summary>
    public static class EarningCalculator
    {
        public static IReadOnlyList<PointsAward> Evaluate(
            OrderEarningContext order,
            IReadOnlyList<EarningRuleSnapshot> rules,
            decimal tierMultiplier,
            DateTime nowUtc)
        {
            var awards = new List<PointsAward>();

            foreach (var rule in rules.OrderBy(r => r.Priority).ThenBy(r => r.RuleVersionId))
            {
                var basePoints = ComputeBase(rule, order);
                if (rule.Multiplier is { } ruleMultiplier) basePoints *= ruleMultiplier;

                var points = WalletMath.Round(basePoints * tierMultiplier);
                if (points <= 0) continue; // non-qualifying or zero-value rule contributes nothing

                awards.Add(new PointsAward
                {
                    RuleVersionId = rule.RuleVersionId,
                    Points = points,
                    ApprovalMode = rule.ApprovalMode,
                    IsPending = IsPending(rule.ApprovalMode),
                    ExpiresAt = ComputeExpiry(rule.ExpirationMode, rule.ExpirationValue, nowUtc)
                });
            }

            return awards;
        }

        private static decimal ComputeBase(EarningRuleSnapshot r, OrderEarningContext o) => r.RuleType switch
        {
            EarningRuleType.FixedPerOrder => r.PointsValue ?? 0m,
            // Basis = order total, matching the legacy AddLoyaltyPointsAsync behavior (no regression).
            EarningRuleType.PerAmount => o.Total * (r.PointsPerCurrencyUnit ?? 0m),
            EarningRuleType.PerCategory => MatchAmount(o, l => l.CategoryId == r.TargetCategoryId, r),
            EarningRuleType.PerProduct => MatchAmount(o, l => l.ProductId == r.TargetProductId, r),
            EarningRuleType.ChannelBonus => o.Source == r.ChannelScope
                ? (r.PointsValue ?? 0m) + o.Total * (r.PointsPerCurrencyUnit ?? 0m)
                : 0m,
            EarningRuleType.FirstOrder => o.IsFirstOrder ? (r.PointsValue ?? 0m) : 0m,
            EarningRuleType.Birthday => o.IsBirthday ? (r.PointsValue ?? 0m) : 0m,
            // Campaign gating (priority/conflict) arrives in Phase 4; here it earns like an amount/flat rule.
            EarningRuleType.Campaign => (r.PointsValue ?? 0m) + o.Total * (r.PointsPerCurrencyUnit ?? 0m),
            _ => 0m
        };

        private static decimal MatchAmount(OrderEarningContext o, Func<OrderLine, bool> predicate, EarningRuleSnapshot r)
        {
            var matchedTotal = o.Lines.Where(predicate).Sum(l => l.LineTotal);
            if (matchedTotal <= 0) return 0m;
            return (r.PointsValue ?? 0m) + matchedTotal * (r.PointsPerCurrencyUnit ?? 0m);
        }

        private static bool IsPending(PointsApprovalMode mode)
            // Engine runs at payment time, so Immediate and OnPaymentConfirmed are available now;
            // OnOrderCompleted and Delayed stay pending until a later approval step.
            => mode is PointsApprovalMode.OnOrderCompleted or PointsApprovalMode.Delayed;

        private static DateTime? ComputeExpiry(PointsExpirationMode mode, int? value, DateTime now) => mode switch
        {
            PointsExpirationMode.Days when value is { } d => now.AddDays(d),
            PointsExpirationMode.Months when value is { } m => now.AddMonths(m),
            PointsExpirationMode.Years when value is { } y => now.AddYears(y),
            _ => null
        };
    }
}
