using System.Globalization;
using RestaurantPos.Api.DTOs.Hr;
using RestaurantPos.Api.DTOs.MobileRequests;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services
{
    public class MobileLoanRequestService : IMobileLoanRequestService
    {
        private readonly IMobileHrRequestRepository _repository;
        private readonly ILogger<MobileLoanRequestService> _logger;

        public MobileLoanRequestService(
            IMobileHrRequestRepository repository,
            ILogger<MobileLoanRequestService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<MobileLoanRequestDto>> GetAsync(
            Guid userId,
            MobileRequestListQuery query,
            CancellationToken ct)
        {
            var staff = await GetStaffAsync(userId, ct);
            var filter = MobileRequestValidation.BuildFilter(query);
            return await _repository.GetLoanRequestsAsync(staff.StaffProfileId, filter, ct);
        }

        public async Task<MobileLoanRequestDto> CreateAsync(
            Guid userId,
            MobileLoanRequestCreateDto request,
            CancellationToken ct)
        {
            var staff = await GetStaffAsync(userId, ct);
            var entity = await BuildEntityAsync(staff, request, ct);
            await _repository.AddLoanRequestAsync(entity, ct);
            _logger.LogInformation("Mobile loan request {RequestId} created by user {UserId}.", entity.Id, userId);
            return Map(entity);
        }

        public async Task<MobileLoanRequestMetaDto> GetMetaAsync(Guid userId, CancellationToken ct)
        {
            var staff = await GetStaffAsync(userId, ct);
            var startDate = DateOnly.FromDateTime(DateTime.UtcNow);
            var previewAmount = MobileLoanPolicies.MaxRequestedAmount * 0.6m;

            return new MobileLoanRequestMetaDto
            {
                EmploymentStartDate = FormatOptionalDate(staff.StartDate),
                PreviousLoanBalance = await _repository.GetPreviousLoanBalanceAsync(staff.StaffProfileId, ct),
                BasicSalary = GetBasicSalary(staff, null),
                Nationality = null,
                Currency = MobileLoanPolicies.DefaultCurrency,
                InstallmentEndDate = MobileRequestValidation.FormatDate(startDate.AddMonths(MobileLoanPolicies.MaxTenureMonths)),
                NetLoanAmount = previewAmount,
                MonthlyInstallment = CalculateMonthlyInstallment(previewAmount, MobileLoanPolicies.MaxTenureMonths),
                LoanTypes = BuildLoanTypeOptions(),
                OwnerOptions = BuildOwnerOptions(),
                MaxTenureMonths = MobileLoanPolicies.MaxTenureMonths,
                MinRequestedAmount = MobileLoanPolicies.MinRequestedAmount,
                MaxRequestedAmount = MobileLoanPolicies.MaxRequestedAmount,
                LifeExpensesMaxMultipleOfSalary = MobileLoanPolicies.LifeExpensesMaxMultipleOfSalary
            };
        }

        private async Task<MobileLoanRequest> BuildEntityAsync(
            MobileStaffSnapshot staff,
            MobileLoanRequestCreateDto request,
            CancellationToken ct)
        {
            var loanType = MobileRequestValidation.RequireCode(request.LoanType, "loan_type", MobileLoanRequestTypes.All);
            var owner = MobileRequestValidation.RequireCode(request.Owner, "owner", MobileLoanOwners.All);
            var requestedAmount = RequirePositiveAmount(request.RequestedAmount, "requested_amount");
            var months = RequireTenure(request.PaymentPeriodMonths);
            var salary = GetBasicSalary(staff, request.BasicSalary);

            ValidateLoanRules(loanType, owner, requestedAmount, salary, request.DependentName);
            var previousBalance = await ResolvePreviousBalanceAsync(staff.StaffProfileId, request, ct);
            return BuildEntity(staff, request, loanType, owner, requestedAmount, months, salary, previousBalance);
        }

        private static MobileLoanRequest BuildEntity(
            MobileStaffSnapshot staff,
            MobileLoanRequestCreateDto request,
            string loanType,
            string owner,
            decimal requestedAmount,
            int months,
            decimal salary,
            decimal previousBalance)
        {
            var installmentStart = MobileRequestValidation.RequireDate(request.InstallmentStartDate, "installment_start_date");
            var requestDate = MobileRequestValidation.RequireDate(request.RequestDate, "request_date");

            return new MobileLoanRequest
            {
                Id = Guid.NewGuid(),
                TenantId = staff.TenantId,
                StaffProfileId = staff.StaffProfileId,
                RequestDate = requestDate,
                PaymentPeriodMonths = months,
                EmploymentStartDate = ResolveEmploymentStartDate(staff, request),
                PreviousLoanBalance = previousBalance,
                LoanType = loanType,
                RequestedAmount = requestedAmount,
                Currency = NormalizeCurrency(request.Currency),
                InstallmentStartDate = installmentStart,
                InstallmentEndDate = installmentStart.AddMonths(months),
                MarriageDate = ParseMarriageDate(loanType, request.MarriageDate),
                BasicSalary = salary,
                Owner = owner,
                Nationality = MobileRequestValidation.TrimAndCap(request.Nationality, 100),
                DependentName = MobileRequestValidation.TrimAndCap(request.DependentName, 150),
                NetAmount = requestedAmount,
                MonthlyInstallment = CalculateMonthlyInstallment(requestedAmount, months),
                Status = MobileRequestStatuses.Pending,
                CreatedByUserId = staff.UserId,
                CreatedByUserName = staff.FullName ?? staff.Username,
                CreatedAt = DateTime.UtcNow
            };
        }

        private async Task<decimal> ResolvePreviousBalanceAsync(
            Guid staffProfileId,
            MobileLoanRequestCreateDto request,
            CancellationToken ct)
        {
            var storedBalance = await _repository.GetPreviousLoanBalanceAsync(staffProfileId, ct);
            var requestBalance = request.PreviousLoanBalance.GetValueOrDefault();
            return storedBalance > 0m ? storedBalance : Math.Max(0m, requestBalance);
        }

        private static void ValidateLoanRules(
            string loanType,
            string owner,
            decimal amount,
            decimal salary,
            string? dependentName)
        {
            if (amount > MobileLoanPolicies.MaxRequestedAmount)
                throw new MobileRequestValidationException("requested_amount exceeds the allowed maximum.");
            if (amount < MobileLoanPolicies.MinRequestedAmount)
                throw new MobileRequestValidationException("requested_amount is below the allowed minimum.");
            if (owner == MobileLoanOwners.Dependent && string.IsNullOrWhiteSpace(dependentName))
                throw new MobileRequestValidationException("dependent_name is required for dependent requests.");
            if (loanType == MobileLoanRequestTypes.LifeExpensesLoan)
                ValidateLifeExpensesCap(amount, salary);
        }

        private static void ValidateLifeExpensesCap(decimal amount, decimal salary)
        {
            if (salary <= 0m)
                throw new MobileRequestValidationException("basic_salary is required for life expenses loans.");
            if (amount > salary * MobileLoanPolicies.LifeExpensesMaxMultipleOfSalary)
                throw new MobileRequestValidationException("requested_amount exceeds the salary multiple limit.");
        }

        private async Task<MobileStaffSnapshot> GetStaffAsync(Guid userId, CancellationToken ct)
            => await _repository.GetOrCreateStaffSnapshotAsync(userId, ct)
                ?? throw new MobileRequestValidationException("Staff profile was not found.");

        private static decimal RequirePositiveAmount(decimal? amount, string field)
        {
            if (!amount.HasValue || amount.Value <= 0m)
                throw new MobileRequestValidationException($"{field} must be greater than zero.");
            return amount.Value;
        }

        private static int RequireTenure(int? months)
        {
            if (!months.HasValue || months.Value < 1)
                throw new MobileRequestValidationException("payment_period_months must be greater than zero.");
            if (months.Value > MobileLoanPolicies.MaxTenureMonths)
                throw new MobileRequestValidationException("payment_period_months exceeds the allowed maximum.");
            return months.Value;
        }

        private static string NormalizeCurrency(string? currency)
        {
            var value = string.IsNullOrWhiteSpace(currency)
                ? MobileLoanPolicies.DefaultCurrency
                : currency.Trim().ToUpperInvariant();
            return value == MobileLoanPolicies.DefaultCurrency
                ? value
                : throw new MobileRequestValidationException("currency is invalid.");
        }

        private static DateOnly? ResolveEmploymentStartDate(
            MobileStaffSnapshot staff,
            MobileLoanRequestCreateDto request)
            => staff.StartDate.HasValue
                ? DateOnly.FromDateTime(staff.StartDate.Value)
                : ParseOptionalDate(request.EmploymentStartDate, "employment_start_date");

        private static DateOnly? ParseMarriageDate(string loanType, string? value)
        {
            if (loanType != MobileLoanRequestTypes.MarriageLoan)
                return ParseOptionalDate(value, "marriage_date");
            return MobileRequestValidation.RequireDate(value, "marriage_date");
        }

        private static DateOnly? ParseOptionalDate(string? value, string field)
            => string.IsNullOrWhiteSpace(value)
                ? null
                : MobileRequestValidation.RequireDate(value, field);

        private static decimal GetBasicSalary(MobileStaffSnapshot staff, decimal? requestedSalary)
        {
            if (staff.NetSalary > 0m) return staff.NetSalary;
            if (staff.MonthlySalary > 0m) return staff.MonthlySalary;
            return Math.Max(0m, requestedSalary.GetValueOrDefault());
        }

        private static decimal CalculateMonthlyInstallment(decimal amount, int months)
            => decimal.Round(amount / months, 2, MidpointRounding.AwayFromZero);

        private static string? FormatOptionalDate(DateTime? date)
            => date.HasValue ? MobileRequestValidation.FormatDate(DateOnly.FromDateTime(date.Value)) : null;

        private static List<MobileOptionDto> BuildLoanTypeOptions()
            => MobileLoanRequestTypes.AllOrdered
                .Select(code => new MobileOptionDto { Code = code, Label = MobileRequestLabels.LoanType(code) })
                .ToList();

        private static List<MobileOptionDto> BuildOwnerOptions()
            => MobileLoanOwners.AllOrdered
                .Select(code => new MobileOptionDto { Code = code, Label = MobileRequestLabels.LoanOwner(code) })
                .ToList();

        private static MobileLoanRequestDto Map(MobileLoanRequest request)
            => new()
            {
                Id = request.Id.ToString(),
                LoanTypeLabel = MobileRequestLabels.LoanType(request.LoanType),
                LoanType = request.LoanType,
                RequestDate = MobileRequestValidation.FormatDate(request.RequestDate),
                AmountLabel = $"{request.RequestedAmount.ToString("N0", CultureInfo.InvariantCulture)} {request.Currency}",
                StatusLabel = MobileRequestLabels.Status(request.Status),
                PaymentPeriodMonths = request.PaymentPeriodMonths,
                Subtitle = $"{request.PaymentPeriodMonths} months · EMI starts {request.InstallmentStartDate.ToString("MMM yyyy", CultureInfo.InvariantCulture)}"
            };

        // ===== Admin =====

        public async Task<HrRequestPagedResponse<HrLoanRequestDto>> GetAdminListAsync(
            HrRequestAdminFilter filter,
            CancellationToken ct)
        {
            var page = await _repository.GetAdminLoanRequestsAsync(filter, ct);
            return new HrRequestPagedResponse<HrLoanRequestDto>
            {
                Items = page.Items.Select(row => MapAdmin(row.Entity, row.Employee)).ToList(),
                Total = page.Total,
                Page = filter.NormalizedPage,
                PageSize = filter.NormalizedPageSize,
                StatusCounts = page.StatusCounts
            };
        }

        public async Task<HrLoanRequestDto> UpdateStatusAsync(
            Guid requestId,
            string newStatus,
            Guid actingUserId,
            bool isAdmin,
            CancellationToken ct)
        {
            var entity = await _repository.GetLoanByIdAsync(requestId, ct)
                ?? throw new KeyNotFoundException("Loan request not found.");
            HrRequestTransitionGuard.Apply(entity.Status, ref newStatus, entity.CreatedByUserId, actingUserId, isAdmin);

            entity.Status = newStatus;
            entity.ReviewedAt = DateTime.UtcNow;
            entity.ReviewedByUserId = actingUserId;
            await _repository.SaveChangesAsync(ct);

            _logger.LogInformation("Loan request {RequestId} -> {Status} by user {UserId}.", entity.Id, newStatus, actingUserId);

            var employee = await _repository.GetEmployeeForStaffAsync(entity.StaffProfileId, ct)
                ?? new HrRequestEmployeeDto { EmployeeId = entity.StaffProfileId };
            return MapAdmin(entity, employee);
        }

        private static HrLoanRequestDto MapAdmin(MobileLoanRequest e, HrRequestEmployeeDto emp) => new()
        {
            Id = e.Id,
            Employee = emp,
            Status = e.Status,
            StatusLabel = MobileRequestLabels.Status(e.Status),
            CreatedAt = e.CreatedAt,
            ReviewedAt = e.ReviewedAt,
            ReviewedByUserId = e.ReviewedByUserId,
            LoanType = e.LoanType,
            LoanTypeLabel = MobileRequestLabels.LoanType(e.LoanType),
            Owner = e.Owner,
            OwnerLabel = MobileRequestLabels.LoanOwner(e.Owner),
            DependentName = e.DependentName,
            RequestDate = MobileRequestValidation.FormatDate(e.RequestDate),
            RequestedAmount = e.RequestedAmount,
            MonthlyInstallment = e.MonthlyInstallment,
            Currency = e.Currency,
            PaymentPeriodMonths = e.PaymentPeriodMonths,
            InstallmentStartDate = MobileRequestValidation.FormatDate(e.InstallmentStartDate),
            InstallmentEndDate = e.InstallmentEndDate.HasValue
                ? MobileRequestValidation.FormatDate(e.InstallmentEndDate.Value)
                : null,
            MarriageDate = e.MarriageDate.HasValue
                ? MobileRequestValidation.FormatDate(e.MarriageDate.Value)
                : null,
            BasicSalary = e.BasicSalary
        };
    }
}
