using System.Globalization;
using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.Jobs
{
    /// <summary>Customer facts a segment rule set is evaluated against (pure input — no EF types).</summary>
    public sealed record SegmentEvaluationSnapshot(
        Guid CustomerId,
        decimal LifetimeSpend,
        int TotalOrders,
        int TotalVisits,
        decimal AvailablePoints,
        Guid? TierId,
        DateTime LastVisit);

    /// <summary>
    /// Pure, fully testable segment-rule evaluation. Predicates in the same LogicGroup are OR'd;
    /// groups are AND'd. Only fields in <see cref="IsSupported"/> are evaluable; anything else never
    /// matches (and is rejected at write time by <c>SegmentService</c>).
    /// </summary>
    public static class SegmentEvaluator
    {
        public static bool IsSupported(SegmentField field) => field switch
        {
            SegmentField.LifetimeSpend => true,
            SegmentField.TotalOrders => true,
            SegmentField.TotalVisits => true,
            SegmentField.LastVisitDaysAgo => true,
            SegmentField.AvailablePoints => true,
            SegmentField.TierId => true,
            _ => false // PreferredChannel and any future field until it has a data source
        };

        public static bool Matches(SegmentEvaluationSnapshot c, IReadOnlyList<SegmentRule> rules, DateTime nowUtc)
        {
            if (rules.Count == 0) return false; // no criteria ⇒ no dynamic members
            foreach (var group in rules.GroupBy(r => r.LogicGroup))
                if (!group.Any(r => EvaluateRule(c, r, nowUtc)))
                    return false;
            return true;
        }

        public static bool EvaluateRule(SegmentEvaluationSnapshot c, SegmentRule rule, DateTime nowUtc) => rule.Field switch
        {
            SegmentField.LifetimeSpend => CompareNumber(c.LifetimeSpend, rule),
            SegmentField.TotalOrders => CompareNumber(c.TotalOrders, rule),
            SegmentField.TotalVisits => CompareNumber(c.TotalVisits, rule),
            SegmentField.AvailablePoints => CompareNumber(c.AvailablePoints, rule),
            SegmentField.LastVisitDaysAgo => CompareNumber((decimal)(nowUtc - c.LastVisit).TotalDays, rule),
            SegmentField.TierId => CompareGuid(c.TierId, rule),
            _ => false
        };

        private static bool CompareNumber(decimal actual, SegmentRule rule)
        {
            if (rule.Operator is SegmentOperator.In or SegmentOperator.NotIn)
            {
                var set = ParseNumberSet(rule.Value);
                var contained = set.Contains(actual);
                return rule.Operator == SegmentOperator.In ? contained : !contained;
            }

            if (!decimal.TryParse(rule.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var target))
                return false;

            return rule.Operator switch
            {
                SegmentOperator.Equals => actual == target,
                SegmentOperator.NotEquals => actual != target,
                SegmentOperator.GreaterThan => actual > target,
                SegmentOperator.GreaterThanOrEqual => actual >= target,
                SegmentOperator.LessThan => actual < target,
                SegmentOperator.LessThanOrEqual => actual <= target,
                _ => false
            };
        }

        private static bool CompareGuid(Guid? actual, SegmentRule rule)
        {
            var actualStr = actual?.ToString();
            var values = rule.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return rule.Operator switch
            {
                SegmentOperator.Equals => string.Equals(actualStr, rule.Value.Trim(), StringComparison.OrdinalIgnoreCase),
                SegmentOperator.NotEquals => !string.Equals(actualStr, rule.Value.Trim(), StringComparison.OrdinalIgnoreCase),
                SegmentOperator.In => actualStr != null && values.Contains(actualStr, StringComparer.OrdinalIgnoreCase),
                SegmentOperator.NotIn => actualStr == null || !values.Contains(actualStr, StringComparer.OrdinalIgnoreCase),
                _ => false
            };
        }

        private static HashSet<decimal> ParseNumberSet(string value)
            => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(v => decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : (decimal?)null)
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToHashSet();
    }
}
