using System.Text.Json.Serialization;

namespace RestaurantPos.Api.DTOs.MobileRequests
{
    public sealed class MobileDataResponse<T>
    {
        public MobileDataResponse(T data)
        {
            Data = data;
        }

        [JsonPropertyName("data")]
        public T Data { get; }
    }

    public sealed class MobileRequestListQuery
    {
        public string? Status { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public int? Page { get; set; }
        public int? PageSize { get; set; }
    }

    public sealed record MobileRequestFilter(
        string? Status,
        DateOnly? From,
        DateOnly? To,
        int Page,
        int PageSize);

    public sealed class MobileLeaveRequestDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("start_date")]
        public string StartDate { get; set; } = string.Empty;

        [JsonPropertyName("end_date")]
        public string EndDate { get; set; } = string.Empty;

        [JsonPropertyName("duration_in_days")]
        public int DurationInDays { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    public sealed class MobileLeaveRequestCreateDto
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("start_date")]
        public string? StartDate { get; set; }

        [JsonPropertyName("end_date")]
        public string? EndDate { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }

    public sealed class MobileLeaveRequestMetaDto
    {
        [JsonPropertyName("annual_balance_days")]
        public int AnnualBalanceDays { get; set; }

        [JsonPropertyName("sick_requires_attachment")]
        public bool SickRequiresAttachment { get; set; }

        [JsonPropertyName("rules_html")]
        public string RulesHtml { get; set; } = string.Empty;

        [JsonPropertyName("max_consecutive_days")]
        public int? MaxConsecutiveDays { get; set; }
    }

    public sealed class MobileLoanRequestDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("loan_type_label")]
        public string LoanTypeLabel { get; set; } = string.Empty;

        [JsonPropertyName("loan_type")]
        public string LoanType { get; set; } = string.Empty;

        [JsonPropertyName("request_date")]
        public string RequestDate { get; set; } = string.Empty;

        [JsonPropertyName("amount_label")]
        public string AmountLabel { get; set; } = string.Empty;

        [JsonPropertyName("status_label")]
        public string StatusLabel { get; set; } = string.Empty;

        [JsonPropertyName("payment_period_months")]
        public int PaymentPeriodMonths { get; set; }

        [JsonPropertyName("subtitle")]
        public string Subtitle { get; set; } = string.Empty;
    }

    public sealed class MobileLoanRequestCreateDto
    {
        [JsonPropertyName("request_date")]
        public string? RequestDate { get; set; }

        [JsonPropertyName("payment_period_months")]
        public int? PaymentPeriodMonths { get; set; }

        [JsonPropertyName("employment_start_date")]
        public string? EmploymentStartDate { get; set; }

        [JsonPropertyName("previous_loan_balance")]
        public decimal? PreviousLoanBalance { get; set; }

        [JsonPropertyName("loan_type")]
        public string? LoanType { get; set; }

        [JsonPropertyName("requested_amount")]
        public decimal? RequestedAmount { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("installment_start_date")]
        public string? InstallmentStartDate { get; set; }

        [JsonPropertyName("marriage_date")]
        public string? MarriageDate { get; set; }

        [JsonPropertyName("basic_salary")]
        public decimal? BasicSalary { get; set; }

        [JsonPropertyName("owner")]
        public string? Owner { get; set; }

        [JsonPropertyName("nationality")]
        public string? Nationality { get; set; }

        [JsonPropertyName("dependent_name")]
        public string? DependentName { get; set; }

        [JsonPropertyName("net_amount")]
        public decimal? NetAmount { get; set; }

        [JsonPropertyName("monthly_installment")]
        public decimal? MonthlyInstallment { get; set; }
    }

    public sealed class MobileLoanRequestMetaDto
    {
        [JsonPropertyName("employment_start_date")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? EmploymentStartDate { get; set; }

        [JsonPropertyName("previous_loan_balance")]
        public decimal PreviousLoanBalance { get; set; }

        [JsonPropertyName("basic_salary")]
        public decimal BasicSalary { get; set; }

        [JsonPropertyName("nationality")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Nationality { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonPropertyName("installment_end_date")]
        public string InstallmentEndDate { get; set; } = string.Empty;

        [JsonPropertyName("net_loan_amount")]
        public decimal NetLoanAmount { get; set; }

        [JsonPropertyName("monthly_installment")]
        public decimal MonthlyInstallment { get; set; }

        [JsonPropertyName("loan_types")]
        public List<MobileOptionDto> LoanTypes { get; set; } = new();

        [JsonPropertyName("owner_options")]
        public List<MobileOptionDto> OwnerOptions { get; set; } = new();

        [JsonPropertyName("max_tenure_months")]
        public int MaxTenureMonths { get; set; }

        [JsonPropertyName("min_requested_amount")]
        public decimal MinRequestedAmount { get; set; }

        [JsonPropertyName("max_requested_amount")]
        public decimal MaxRequestedAmount { get; set; }

        [JsonPropertyName("life_expenses_max_multiple_of_salary")]
        public decimal LifeExpensesMaxMultipleOfSalary { get; set; }
    }

    public sealed class MobileOptionDto
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;
    }

    public sealed class MobilePermissionRequestListItemDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("type_label")]
        public string TypeLabel { get; set; } = string.Empty;

        [JsonPropertyName("permission_type")]
        public string PermissionType { get; set; } = string.Empty;

        [JsonPropertyName("time_from")]
        public string TimeFrom { get; set; } = string.Empty;

        [JsonPropertyName("time_to")]
        public string TimeTo { get; set; } = string.Empty;

        [JsonPropertyName("time_range")]
        public string TimeRange { get; set; } = string.Empty;

        [JsonPropertyName("status_label")]
        public string StatusLabel { get; set; } = string.Empty;

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    public sealed class MobilePermissionRequestDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("permission_type")]
        public string PermissionType { get; set; } = string.Empty;

        [JsonPropertyName("time_from")]
        public string TimeFrom { get; set; } = string.Empty;

        [JsonPropertyName("time_to")]
        public string TimeTo { get; set; } = string.Empty;

        [JsonPropertyName("total_duration")]
        public string TotalDuration { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    public class MobilePermissionRequestCreateDto
    {
        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("permission_type")]
        public string? PermissionType { get; set; }

        [JsonPropertyName("time_from")]
        public string? TimeFrom { get; set; }

        [JsonPropertyName("time_to")]
        public string? TimeTo { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    public sealed class MobilePermissionAllowanceDto
    {
        [JsonPropertyName("remaining_hours")]
        public string RemainingHours { get; set; } = string.Empty;

        [JsonPropertyName("period_start")]
        public string PeriodStart { get; set; } = string.Empty;

        [JsonPropertyName("period_end")]
        public string PeriodEnd { get; set; } = string.Empty;
    }
}
