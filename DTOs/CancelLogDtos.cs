using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs
{
    /// <summary>Query filter for GET /api/cancel-log.</summary>
    public class CancelLogQueryDto
    {
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public Guid? OrderId { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 50;
    }

    /// <summary>Paged result for the cancel-log list. Field names preserved 1:1 with
    /// the original anonymous response (data / total / page / limit) so the frontend
    /// payload shape is unchanged.</summary>
    public class CancelLogPageDto
    {
        public List<CancelLogRowDto> Data { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int Limit { get; set; }
    }

    /// <summary>One row of the cancel-log list. Camel-cased property names match the
    /// original anonymous-object response 1:1 (id, orderId, orderNumber, …).</summary>
    public class CancelLogRowDto
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public Guid? OrderItemId { get; set; }
        public bool IsFullOrder { get; set; }
        public string? ItemName { get; set; }
        public string? ItemNameAr { get; set; }
        public string? CancelledByName { get; set; }
        public string? CancelledByRole { get; set; }
        public DateTime OrderTime { get; set; }
        public DateTime CancelledAt { get; set; }
        public string? CancelMode { get; set; }
        public string? CancelReasonCode { get; set; }
        public string? CancelReasonLabel { get; set; }
        public string? CancelReasonLabelAr { get; set; }
        public string? CancelReasonNote { get; set; }
        public bool HasWaste { get; set; }
        public Guid? WasteLogId { get; set; }
        public bool RequiresKitchenApproval { get; set; }
        public string? ApprovalStatus { get; set; }
        public string? KitchenDecision { get; set; }
        public string? KitchenDecisionByName { get; set; }
        public DateTime? KitchenDecisionAt { get; set; }
    }

    /// <summary>Public read DTO for a cancel reason. Was previously a controller-private
    /// record; lifted to DTOs/ so the service can return it without a duplicate type.</summary>
    public class CancelReasonDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public bool RequiresNote { get; set; }
    }

    /// <summary>Write DTO for POST /api/cancel-reasons and PUT /api/cancel-reasons/{id}.</summary>
    public class CancelReasonUpsertRequest
    {
        [Required, StringLength(50, MinimumLength = 1)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(200, MinimumLength = 1)]
        public string NameAr { get; set; } = string.Empty;

        public bool RequiresNote { get; set; }
        public bool IsActive { get; set; } = true;
        public int? SortOrder { get; set; }
    }

    /// <summary>Detail DTO returned by POST/PUT on a cancel reason. Field shape mirrors
    /// the previous anonymous-object response 1:1.</summary>
    public class CancelReasonDetailDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public bool RequiresNote { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
    }
}
