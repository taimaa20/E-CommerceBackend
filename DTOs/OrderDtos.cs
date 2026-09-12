using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs
{
    public class OrderCreateDto
    {
        // TenantId & Table info
        public Guid TenantId { get; set; }
        public Guid? TableId { get; set; } // Changed to Guid? to match Entity
        public string TableName { get; set; } // Added TableName
        public int OrderType { get; set; } = 0; // 0=DineIn, 1=Takeaway

        // We can generate OrderNumber backend side, or accept it if offline-first
        // public string OrderNumber { get; set; } 
        
        // Ticket ID: one ticket per table session (persists across multiple orders)
        // Auto-generated on first order creation in session, reused for subsequent orders
        public int TicketId { get; set; } = 0;
        
        public decimal DiscountPercentage { get; set; } = 0;
        public decimal ServiceChargeRate { get; set; } = 0;
        public decimal TaxRate { get; set; } = 0;

        // Affiliation discount group selected by the cashier (verified in person). Only the
        // Id is sent; the backend loads the group, validates tenant + active state, and
        // resolves the authoritative percentage. A client-supplied percentage is never trusted.
        public Guid? DiscountGroupId { get; set; }

        // P1 (offline financial preservation): exact amounts captured at the
        // moment of an offline order. The server honors these ONLY when
        // PublicOrderNumber is set (i.e. an OFF# offline order being
        // replayed) so admin rate / price edits during the outage window
        // can never retroactively move the recorded totals away from the
        // printed receipt and the cash the customer paid. Online orders
        // ignore these fields and continue to compute totals server-side.
        public decimal? Subtotal { get; set; }
        public decimal? DiscountAmount { get; set; }
        public decimal? ServiceChargeAmount { get; set; }
        public decimal? TaxAmount { get; set; }
        public decimal? TotalAmount { get; set; }
        public DateTime? OfflinePaidAt { get; set; }
        public int? OfferId { get; set; }
        public int? OfferQty { get; set; }
        public string? OfferNote { get; set; }

        public Guid? CustomerId { get; set; }
        public string? CustomerPhone { get; set; }
        [MaxLength(200)]
        public string? CustomerName { get; set; }
        [EmailAddress, MaxLength(200)]
        public string? CustomerEmail { get; set; }
        [MaxLength(50)]
        public string? PaymentMethod { get; set; }
        // Transfer/transaction reference supplied by the customer, required only when the
        // chosen PaymentMethod.RequiresReferenceNumber says so. Snapshot of what they declared
        // at checkout — the settled Payment keeps its own ReferenceNumber when staff take it.
        [MaxLength(120)]
        public string? PaymentReferenceNumber { get; set; }
        public DateTime? ScheduledFor { get; set; }

        [JsonPropertyName("clientOrderUUID")]
        public string? ClientOrderUuid { get; set; }

        [JsonPropertyName("publicOrderNumber")]
        public string? PublicOrderNumber { get; set; }

        public string? OrderSource { get; set; }
        public string? TalabatOrderNumber { get; set; }
        public string? TalabatCustomerName { get; set; }
        public string? TalabatCustomerPhone { get; set; }
        public string? TalabatPaymentMethod { get; set; }
        public DateTime? TalabatPickupTime { get; set; }
        public decimal? TalabatDeliveryFee { get; set; }
        public decimal? TalabatServiceFee { get; set; }
        public Guid? DeliveryPartnerId { get; set; }
        public string? PartnerOrderNumber { get; set; }
        public string? PartnerCustomerName { get; set; }
        public string? PartnerCustomerPhone { get; set; }
        public string? PartnerPaymentMethod { get; set; }
        public DateTime? PartnerPickupTime { get; set; }
        public decimal? PartnerDeliveryFee { get; set; }
        public decimal? PartnerServiceFee { get; set; }
        public decimal? ActualDeliveryCost { get; set; }
        public bool HasPriceDifference { get; set; }
        public string? PriceDifferenceReasonCode { get; set; }
        public string? PriceDifferenceNote { get; set; }
        public string? PriceDifferenceCorrectionMode { get; set; }
        public decimal? CorrectPartnerTotal { get; set; }

        // Delivery (used only when OrderType == 2/Delivery).
        // Fee/cost/name/payment-mode are resolved server-side from the zone snapshot — never trusted from the client.
        public Guid? DeliveryZoneId { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? DeliveryNotes { get; set; }
        // Structured address parts accepted from the public storefront. They are composed into
        // DeliveryAddress at that boundary rather than stored separately, so every existing
        // reader of an order — POS board, printed ticket, receipt — keeps one complete line.
        [MaxLength(120)]
        public string? DeliveryBuildingNo { get; set; }
        [MaxLength(160)]
        public string? DeliveryStreet { get; set; }
        [MaxLength(120)]
        public string? DeliveryFloor { get; set; }
        // Optional manual fee override; honored only for Admin/Manager (enforced server-side). Null => use zone fee.
        public decimal? DeliveryFeeOverride { get; set; }
        // P4 (offline preservation): historical fee/cost snapshot captured by
        // the cashier at the moment of an offline order. The server only honors
        // these when PublicOrderNumber is set (i.e. an OFF# offline order being
        // replayed) — they exist so admin price edits during the outage window
        // cannot retroactively change the total against the printed receipt.
        // Online orders ignore these fields and continue to use the live zone.
        public decimal? DeliveryFee { get; set; }
        public decimal? DeliveryCost { get; set; }

        public List<OrderItemCreateDto> Items { get; set; } = new();
    }

    public class OrderItemCreateDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string? Notes { get; set; }
        public bool IsComplimentary { get; set; } = false;
        public int? OfferLineId { get; set; }
        public string? OfferLineName { get; set; }
        public string? OfferLineNameAr { get; set; }
        public decimal? PartnerCorrectedUnitPrice { get; set; }

        /// <summary>Selected product option (variant). When set, overrides product price and recipe.</summary>
        public Guid? SelectedOptionId { get; set; }

        /// <summary>Per-ingredient swap choices. Each entry replaces exactly ONE RecipeItem
        /// in the product's default recipe — the rest of the recipe is unchanged. Ignored
        /// when <see cref="SelectedOptionId"/> is set (option takes precedence).</summary>
        public List<SelectedRecipeAlternativeDto> SelectedRecipeAlternatives { get; set; } = new();

        public List<OrderItemModifierCreateDto> Modifiers { get; set; } = new();
    }

    public class OrderItemUpdateDto
    {
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string? Notes { get; set; }
        public bool IsComplimentary { get; set; } = false;

        public List<OrderItemModifierCreateDto> Modifiers { get; set; } = new();

        /// <summary>When omitted, the existing persisted snapshot's alternatives are preserved.</summary>
        public List<SelectedRecipeAlternativeDto> SelectedRecipeAlternatives { get; set; } = new();
    }

    public class PartnerDiscountOverrideRequest
    {
        public bool HasPartnerDiscountOverride { get; set; }

        public decimal? PartnerOriginalUnitPrice { get; set; }

        public decimal? PartnerDiscountedUnitPrice { get; set; }

        [MaxLength(60)]
        public string? ReasonCode { get; set; }

        [MaxLength(500)]
        public string? PartnerDiscountReason { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }
    }

    public class PartnerDiscountOverrideDto
    {
        public Guid OrderId { get; set; }
        public Guid OrderItemId { get; set; }
        public bool HasPartnerDiscountOverride { get; set; }
        public decimal? PartnerOriginalUnitPrice { get; set; }
        public decimal? PartnerDiscountedUnitPrice { get; set; }
        public string? PartnerDiscountReason { get; set; }
        public DateTime? PartnerDiscountUpdatedAt { get; set; }
        public Guid? PartnerDiscountUpdatedBy { get; set; }
        public string? PartnerDiscountUpdatedByName { get; set; }
    }

    public class DeliveryPartnerPriceAdjustmentRequest
    {
        public bool HasPriceDifference { get; set; }

        [Required]
        [MaxLength(60)]
        public string ReasonCode { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Note { get; set; }

        [MaxLength(20)]
        public string? CorrectionMode { get; set; }

        public decimal? CorrectPartnerTotal { get; set; }

        public List<DeliveryPartnerPriceAdjustmentItemRequest> Items { get; set; } = new();
    }

    public class DeliveryPartnerPriceAdjustmentItemRequest
    {
        public Guid OrderItemId { get; set; }
        public decimal NewUnitPrice { get; set; }
    }

    public class DeliveryPartnerPriceAdjustmentDto
    {
        public Guid OrderId { get; set; }
        public bool HasPriceDifference { get; set; }
        public string ReasonCode { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string CorrectionMode { get; set; } = "PerItem";
        public decimal? OriginalPartnerTotal { get; set; }
        public decimal? CorrectPartnerTotal { get; set; }
        public decimal? TotalDifferenceAmount { get; set; }
        public decimal PreviousTotalAmount { get; set; }
        public decimal NewTotalAmount { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public string? UpdatedByName { get; set; }
        public List<DeliveryPartnerPriceAdjustmentItemDto> Items { get; set; } = new();
    }

    public class DeliveryPartnerPriceAdjustmentItemDto
    {
        public Guid OrderItemId { get; set; }
        public decimal PreviousPrice { get; set; }
        public decimal NewPrice { get; set; }
        public decimal PreviousDiscount { get; set; }
        public decimal NewDiscount { get; set; }
    }

    public class OrderTotalOverrideRequest
    {
        public decimal CorrectedTotalAmount { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    public class OrderTotalOverrideDto
    {
        public Guid OrderId { get; set; }
        public decimal PreviousTotalAmount { get; set; }
        public decimal CorrectedTotalAmount { get; set; }
        public string? Reason { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public string? UpdatedByName { get; set; }
    }

    public class PartnerPriceOverrideAuditDto
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Guid OrderItemId { get; set; }
        public decimal? OldPrice { get; set; }
        public decimal? NewPrice { get; set; }
        public decimal? OldDiscountAmount { get; set; }
        public decimal? NewDiscountAmount { get; set; }
        public string? ReasonCode { get; set; }
        public string? Reason { get; set; }
        public string? Note { get; set; }
        public Guid? DeliveryPartnerId { get; set; }
        public string? DeliveryPartnerName { get; set; }
        public string? DeliveryPartnerNameAr { get; set; }
        public string? DeliveryPartnerCode { get; set; }
        public string? CorrectionMode { get; set; }
        public decimal? OriginalTotal { get; set; }
        public decimal? CorrectTotal { get; set; }
        public decimal? DifferenceAmount { get; set; }
        public Guid? UpdatedBy { get; set; }
        public string? UpdatedByName { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class OrderItemModifierCreateDto
    {
        public Guid? ModifierId { get; set; }
        public string ModifierName { get; set; }
        public string? ModifierNameAr { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class OrderItemRecipeSnapshotDto
    {
        public Guid RawMaterialId { get; set; }
        public Guid? SourceModifierId { get; set; }
        public Guid? SourceOptionId { get; set; }
        /// <summary>When set, this row came from an ingredient swap; the original RecipeItem
        /// was replaced by the alternative identified by <see cref="SourceAlternativeId"/>.</summary>
        public Guid? SourceRecipeItemId { get; set; }
        public Guid? SourceAlternativeId { get; set; }
        /// <summary>Snapshot of the alternative's display name at order time.</summary>
        public string? SourceAlternativeName { get; set; }
        public string? SourceAlternativeNameAr { get; set; }
        public string RawMaterialName { get; set; } = string.Empty;
        public string? RawMaterialNameAr { get; set; }
        public decimal Quantity { get; set; }
    }

    public class OrderDto 
    {
        public Guid Id { get; set; }
        // OrderNumber carries the user-facing identifier (DisplayOrderNumber when
        // present, then PublicOrderNumber, then the raw internal OrderNumber) so
        // existing frontends keep working unchanged. DisplayOrderNumber is also
        // exposed separately for callers that want the new value explicitly.
        public string OrderNumber { get; set; }
        public string? DisplayOrderNumber { get; set; }
        // Raw internal identifier (T-yyyyMMddHHmmss) — surfaced separately so the
        // cashier/payment views can show both the user-facing DisplayOrderNumber
        // and the system reference without re-deriving from OrderNumber, which is
        // already overloaded to prefer the display value.
        public string? SystemOrderNumber { get; set; }
        [JsonPropertyName("clientOrderUUID")]
        public string? ClientOrderUuid { get; set; }
        public string? PublicOrderNumber { get; set; }
        public int TicketId { get; set; } // Session ticket ID (one per table session; multiple orders share this)
        public string TableName { get; set; }
        public string OrderType { get; set; } = "DineIn"; // "DineIn" or "Takeaway"
        public string OrderSource { get; set; } = "Pos";
        public string? TalabatOrderNumber { get; set; }
        public string? TalabatCustomerName { get; set; }
        public string? TalabatCustomerPhone { get; set; }
        public string? TalabatPaymentMethod { get; set; }
        public DateTime? TalabatPickupTime { get; set; }
        public decimal? TalabatDeliveryFee { get; set; }
        public decimal? TalabatServiceFee { get; set; }
        public Guid? DeliveryPartnerId { get; set; }
        public string? DeliveryPartnerName { get; set; }
        public string? DeliveryPartnerNameAr { get; set; }
        public string? DeliveryPartnerCode { get; set; }
        public string? PartnerOrderNumber { get; set; }
        public string? PartnerCustomerName { get; set; }
        public string? PartnerCustomerPhone { get; set; }
        public string? PartnerPaymentMethod { get; set; }
        public DateTime? PartnerPickupTime { get; set; }
        public decimal? PartnerDeliveryFee { get; set; }
        public decimal? PartnerServiceFee { get; set; }
        public int? OfferId { get; set; }
        public string? OfferName { get; set; }
        public string? OfferNameAr { get; set; }
        public string? OfferNote { get; set; }
        public List<OrderOfferProductDto> OfferProducts { get; set; } = new();

        // Breakdown fields (all calculated server-side)
        public decimal Subtotal { get; set; }          // Sum of item prices before any charges
        public decimal DiscountPercentage { get; set; }
        public decimal DiscountAmount { get; set; }    // Subtotal × DiscountPercentage / 100
        public Guid? DiscountGroupId { get; set; }              // Affiliation discount group applied (snapshot reference)
        public string? DiscountGroupName { get; set; }          // Snapshot of the group name at apply time
        public DiscountValueType DiscountGroupType { get; set; } // Percentage or FixedAmount
        public decimal DiscountGroupValue { get; set; }          // Configured value: % or flat amount
        public decimal DiscountGroupAmount { get; set; }         // Computed applied amount
        public decimal ServiceChargeRate { get; set; }
        public decimal ServiceChargeAmount { get; set; } // Applied on discounted subtotal
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }         // Applied on (discounted subtotal + service)
        public bool IsVoucherApplied { get; set; }
        public decimal VoucherDiscountAmount { get; set; }
        public DateTime? VoucherAppliedAt { get; set; }
        public decimal TotalAmount { get; set; }       // Final amount due
        public decimal FoodSubtotal { get; set; }
        public decimal CustomerDeliveryFee { get; set; }
        public decimal ActualDeliveryCost { get; set; }
        public decimal DeliveryMargin { get; set; }
        public decimal MarketplaceDeliveryFee { get; set; }
        public decimal MarketplaceServiceFee { get; set; }
        public decimal NetRestaurantRevenue { get; set; }
        public decimal CostSharingTotalCommission { get; set; }
        public decimal CostSharingRestaurantShare { get; set; }
        public decimal CostSharingCounterpartyShare { get; set; }
        public decimal CostSharingNetSettlement { get; set; }
        public DateTime? CostSharingCalculatedAt { get; set; }
         public string Status { get; set; }
         public bool IsPaid { get; set; }
        public string PaymentStatus { get; set; } = "Pending";
        public string? PaymentMethod { get; set; }
        public string? CashierName { get; set; }
        public Guid? CreatedBy { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? CustomerId { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CustomerName { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CustomerNameAr { get; set; }
        public string? CustomerPhone { get; set; }
        public decimal? AmountTendered { get; set; }
        public decimal? ChangeAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CancelMode { get; set; }
        public Guid? CanceledById { get; set; }
        public string? CanceledByName { get; set; }
        public DateTime? CanceledAt { get; set; }
        public string? CancelReason { get; set; }
        /// <summary>Last time items were added/removed/updated. Null for orders that have never been modified.</summary>
        public DateTime? LastUpdatedAt { get; set; }
        public DateTime? DispatchedAt { get; set; }
        /// <summary>Server-set UTC timestamp of the last confirmed write — used by the offline client to age per-order state.</summary>
        public DateTime? SyncedAt { get; set; }
        public DateTime? ScheduledFor { get; set; }

        // Delivery snapshot (null for non-delivery orders). PaymentMode serialized as its enum name.
        public Guid? DeliveryZoneId { get; set; }
        public string? DeliveryZoneName { get; set; }
        public decimal? DeliveryFee { get; set; }
        public decimal? DeliveryCost { get; set; }
        public string? DeliveryPaymentMode { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? DeliveryNotes { get; set; }
        /// Reference the customer declared at checkout, when their method required one. Distinct
        /// from <see cref="PaymentReferenceNumber"/>, which is derived from settled payments.
        public string? SubmittedPaymentReference { get; set; }

        public List<PaymentDto> Payments { get; set; } = new();
        public List<OrderItemDto> Items { get; set; } = new();
        public int TotalItemsCount { get; set; }
        public bool HasPriceDifference { get; set; }
        public string? PriceDifferenceCorrectionMode { get; set; }
        public decimal? OriginalPartnerTotal { get; set; }
        public decimal? CorrectPartnerTotal { get; set; }
        public decimal? TotalDifferenceAmount { get; set; }
        public bool HasPartnerDiscountOverride { get; set; }
        public bool HasPendingCancellation { get; set; }
        public bool HasPendingFullCancellation { get; set; }
        public List<Guid> PendingCancellationItemIds { get; set; } = new();

        // Populated when the order has a pending full-order cancellation request, so the admin
        // approval popup can show who requested it, the reason, and the cancellation type.
        public CancellationInfoDto? CancellationInfo { get; set; }

        // Refund fields — populated when a Paid order has been refunded (order status becomes Cancelled)
        public bool IsRefunded { get; set; }
        public decimal? RefundAmount { get; set; }
        public DateTime? RefundedAt { get; set; }
    }

    /// <summary>
    /// Snapshot of a pending (or resolved) full-order cancellation request, surfaced on
    /// <see cref="OrderDto"/> so the admin orders popup can drive the approval workflow without a
    /// separate fetch. Mirrors the frontend CancellationInfoDto.
    /// </summary>
    public class CancellationInfoDto
    {
        public Guid CancelLogId { get; set; }
        public Guid? OrderItemId { get; set; }
        public string? ItemName { get; set; }
        public string RequestedBy { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; }
        public string CancellationReason { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string ApprovalStatus { get; set; } = string.Empty;
        public string? ManagerDecision { get; set; }
        public string? ManagerNotes { get; set; }
        public DateTime? DecisionDate { get; set; }
        public string? ApprovedOrRejectedBy { get; set; }
        public string? PreviousStatus { get; set; }
    }

    public class OrderFilterRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Status { get; set; }
        public string? Payment { get; set; }
        public string? Type { get; set; }
        public string? Source { get; set; }
        public Guid? BranchId { get; set; }
        public Guid? PartnerId { get; set; }
        public bool? VoucherApplied { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? Search { get; set; }
        public string? Sort { get; set; } = "newest";
    }

    public class OrderListItemDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int TicketId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public string OrderType { get; set; } = "DineIn";
        public string OrderSource { get; set; } = "Pos";
        public string? TalabatOrderNumber { get; set; }
        public DateTime? TalabatPickupTime { get; set; }
        public Guid? DeliveryPartnerId { get; set; }
        public string? DeliveryPartnerName { get; set; }
        public string? DeliveryPartnerNameAr { get; set; }
        public string? DeliveryPartnerCode { get; set; }
        public string? PartnerOrderNumber { get; set; }
        public int ItemCount { get; set; }
        public int TotalItemsCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal FoodSubtotal { get; set; }
        public decimal CustomerDeliveryFee { get; set; }
        public decimal ActualDeliveryCost { get; set; }
        public decimal DeliveryMargin { get; set; }
        public decimal MarketplaceDeliveryFee { get; set; }
        public decimal MarketplaceServiceFee { get; set; }
        public decimal NetRestaurantRevenue { get; set; }
        public decimal CostSharingTotalCommission { get; set; }
        public decimal CostSharingRestaurantShare { get; set; }
        public decimal CostSharingCounterpartyShare { get; set; }
        public decimal CostSharingNetSettlement { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsPaid { get; set; }
        public string PaymentStatus { get; set; } = "Pending";
        public string? PaymentMethod { get; set; }
        public string? PaymentMethodName { get; set; }
        public string? PaymentMethodNameAr { get; set; }
        public string? PaymentMethodCode { get; set; }
        public string? PaymentReferenceNumber { get; set; }
        public string? CashierName { get; set; }
        public bool IsVoucherApplied { get; set; }
        public decimal VoucherDiscountAmount { get; set; }
        public DateTime? VoucherAppliedAt { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? CustomerId { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CustomerName { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CustomerNameAr { get; set; }
        public string? CustomerPhone { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public bool HasPendingPaymentAdjustment { get; set; }
        public Guid? PendingPaymentAdjustmentId { get; set; }
        public bool HasPendingCancellationApproval { get; set; }
        public Guid? PendingCancellationId { get; set; }
        public bool HasPriceDifference { get; set; }
        public bool HasPartnerDiscountOverride { get; set; }
    }

    public class OrderListSummaryDto
    {
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public int PaidCount { get; set; }
        public decimal Revenue { get; set; }
        public decimal FoodRevenue { get; set; }
        public decimal DeliveryCollected { get; set; }
        public decimal DeliveryCost { get; set; }
        public decimal DeliveryProfit { get; set; }
        public decimal MarketplaceFees { get; set; }
        public decimal MarketplaceServiceFees { get; set; }
        public decimal NetRestaurantRevenue { get; set; }
        public decimal CostSharingTotalCommission { get; set; }
        public decimal CostSharingRestaurantShare { get; set; }
        public decimal CostSharingCounterpartyShare { get; set; }
        public decimal NetRevenueAfterCostSharing { get; set; }
        public int VoucherCount { get; set; }
        public decimal VoucherDiscountTotal { get; set; }
        public decimal PartnerGrossRevenue { get; set; }
        public decimal PartnerDiscountRevenue { get; set; }
        public decimal PartnerDiscountAmount { get; set; }
        public int PartnerDiscountCount { get; set; }
    }

    public class OrderExportRowDto
    {
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal FoodSubtotal { get; set; }
        public decimal CustomerDeliveryFee { get; set; }
        public decimal ActualDeliveryCost { get; set; }
        public decimal DeliveryMargin { get; set; }
        public decimal MarketplaceDeliveryFee { get; set; }
        public decimal MarketplaceServiceFee { get; set; }
        public decimal NetRestaurantRevenue { get; set; }
        public decimal CostSharingTotalCommission { get; set; }
        public decimal CostSharingRestaurantShare { get; set; }
        public decimal CostSharingCounterpartyShare { get; set; }
        public decimal CostSharingNetSettlement { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PaymentReferenceNumber { get; set; }
        public string? CashierName { get; set; }
        public string OrderSource { get; set; } = "Pos";
        public string? TalabatOrderNumber { get; set; }
        public string? PartnerName { get; set; }
        public string? PartnerCode { get; set; }
        public string? PartnerOrderNumber { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class RefundOrderRequest
    {
        /// <summary>Short reason code (e.g. "QUALITY_ISSUE", "WRONG_ORDER", "OTHER").</summary>
        public string RefundReason { get; set; } = string.Empty;
        /// <summary>Required when RefundReason is "OTHER" (min 5 characters).</summary>
        public string? RefundNote { get; set; }
    }

    public class OrderOfferProductDto
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public int Quantity { get; set; }
        // Additive fields — existing clients ignore these; new clients use them
        // to call /orders/{orderId}/items/{orderItemId}/ready on the real row
        // instead of the synthetic bundle (whose Id is Guid.Empty).
        public Guid? OrderItemId { get; set; }
        public bool IsReady { get; set; }
    }

    public class OrderItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public int? OfferLineId { get; set; }
        public string ProductName { get; set; }
        public string? ProductNameAr { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }         // Per-item base price after order-level discount; excludes tax and service charge
        public decimal LineTotalAmount { get; set; }   // (UnitPrice + modifiers) × Quantity; 0 if complimentary
        public decimal? UnitPriceSnapshot { get; set; }
        public decimal? LineTotalSnapshot { get; set; }
        public decimal? PartnerPriceSnapshot { get; set; }
        public bool HasPartnerDiscountOverride { get; set; }
        public decimal? PartnerOriginalUnitPrice { get; set; }
        public decimal? PartnerDiscountedUnitPrice { get; set; }
        public string? PartnerDiscountReason { get; set; }
        public DateTime? PartnerDiscountUpdatedAt { get; set; }
        public Guid? PartnerDiscountUpdatedBy { get; set; }
        public string? PartnerDiscountUpdatedByName { get; set; }
        public string? Notes { get; set; }
        public bool IsComplimentary { get; set; }
        public string ItemStatus { get; set; } = "Prepared";
        public bool IsNewlyAdded { get; set; }         // True = item was added to an existing order (kitchen "NEW" badge)
        public int PreparationStation { get; set; } // 0=Kitchen, 1=Bar
        /// <summary>Snapshot of the selected variant name at order time.</summary>
        public string? SelectedOptionName { get; set; }
        public string? SelectedOptionNameAr { get; set; }
        public List<OrderItemModifierDto> Modifiers { get; set; } = new();
        public List<OrderItemRecipeSnapshotDto> RecipeSnapshot { get; set; } = new();
        public bool IsOfferBundle { get; set; }         // True when this item represents a bundled offer display
        public List<OrderOfferProductDto>? BundledOfferProducts { get; set; }  // Products included in the offer bundle
    }

    public class OrderItemModifierDto
    {
        public Guid? ModifierId { get; set; }
        public string ModifierName { get; set; }
        public string? ModifierNameAr { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class UpdateOfferNoteRequest
    {
        public string? Note { get; set; }
    }

    public class SetOrderOfferRequest
    {
        public int? OfferId { get; set; }
        public string? OfferNote { get; set; }
        public int? OfferQty { get; set; }
    }

    /// <summary>
    /// Lightweight DTO for the public Order Tracker display.
    /// Contains only order number, type, and status — no prices or item details.
    /// </summary>
    public class TrackerOrderDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = "";
        public string OrderType { get; set; } = "DineIn";
        public string Status { get; set; } = "Preparing";
        /// <summary>True when the order has been paid (OrderStatus.Paid). Used by the tracker
        /// to reveal the "Reserved / Picked Up" button for Takeaway orders.</summary>
        public bool IsPaid { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
