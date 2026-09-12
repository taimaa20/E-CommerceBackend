namespace RestaurantPos.Api.Modules.Marketing.Domain
{
    // Append-only enums. Per the contract, values are appended only — never reordered or removed.

    public enum WalletStatus
    {
        Active = 0,
        Frozen = 1,
        Closed = 2
    }

    public enum WalletTransactionType
    {
        Earn = 0,
        EarnPendingApproved = 1,
        Redeem = 2,
        Expire = 3,
        ReverseEarn = 4,
        ReverseRedeem = 5,
        Adjust = 6,
        ReferralBonus = 7,
        CampaignBonus = 8,
        MergeTransferOut = 9,
        MergeTransferIn = 10
    }

    public enum WalletTransactionStatus
    {
        Pending = 0,
        Available = 1,
        Reversed = 2,
        Expired = 3
    }

    public enum WalletTransactionSource
    {
        Order = 0,
        Manual = 1,
        Referral = 2,
        Campaign = 3,
        Promo = 4,
        Reward = 5,
        Signup = 6,
        Birthday = 7,
        Merge = 8
    }

    public enum PointsLotStatus
    {
        Active = 0,
        Depleted = 1,
        Expired = 2
    }

    public enum EarningRuleType
    {
        FixedPerOrder = 0,
        PerAmount = 1,
        PerCategory = 2,
        PerProduct = 3,
        ChannelBonus = 4,
        FirstOrder = 5,
        Birthday = 6,
        Campaign = 7
    }

    public enum PointsApprovalMode
    {
        Immediate = 0,
        OnOrderCompleted = 1,
        OnPaymentConfirmed = 2,
        Delayed = 3
    }

    public enum PointsExpirationMode
    {
        Never = 0,
        Days = 1,
        Months = 2,
        Years = 3
    }

    public enum CustomerSegmentType
    {
        Dynamic = 0,
        Static = 1
    }

    public enum SegmentMemberSource
    {
        Manual = 0,
        Computed = 1,
        Import = 2
    }

    public enum SegmentField
    {
        LifetimeSpend = 0,
        TotalOrders = 1,
        TotalVisits = 2,
        LastVisitDaysAgo = 3,
        AvailablePoints = 4,
        TierId = 5,
        PreferredChannel = 6
    }

    public enum SegmentOperator
    {
        Equals = 0,
        NotEquals = 1,
        GreaterThan = 2,
        GreaterThanOrEqual = 3,
        LessThan = 4,
        LessThanOrEqual = 5,
        In = 6,
        NotIn = 7
    }

    public enum TierBenefitType
    {
        Multiplier = 0,
        FreeDelivery = 1,
        ExclusiveReward = 2,
        DiscountPercentage = 3,
        Custom = 4
    }

    public enum CustomerMergeStatus
    {
        Completed = 0,
        RolledBack = 1
    }

    public enum MarketingAuditAction
    {
        Created = 0,
        Updated = 1,
        Deleted = 2,
        Frozen = 3,
        Unfrozen = 4,
        Closed = 5,
        Merged = 6,
        MergeRolledBack = 7,
        RuleVersioned = 8,
        PointsEarned = 9,
        PointsRedeemed = 10,
        PointsReversed = 11,
        PointsExpired = 12
    }
}
