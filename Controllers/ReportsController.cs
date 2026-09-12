using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Api.Services;
using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;
        private readonly IOrderMetricsService _orderMetricsService;

        public ReportsController(IReportService reportService, IOrderMetricsService orderMetricsService)
        {
            _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
            _orderMetricsService = orderMetricsService ?? throw new ArgumentNullException(nameof(orderMetricsService));
        }

        [HttpGet("daily-summary")]
        public async Task<IActionResult> GetDailySummary([FromQuery] Guid? branchId, CancellationToken ct)
        {
            return Ok(await _orderMetricsService.GetDailySummaryAsync(branchId, ct));
        }

        [HttpGet("z-report")]
        public async Task<IActionResult> GetZReport(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] Guid? branchId,
            CancellationToken ct)
        {
            return Ok(await _orderMetricsService.GetZReportAsync(startDate, endDate, branchId, ct));
        }

        [HttpGet("profit-loss")]
        public async Task<IActionResult> GetProfitLossReport(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] Guid? branchId,
            CancellationToken ct)
        {
            var start = startDate?.Date ?? DateTime.UtcNow.Date.AddDays(-30);
            var end = endDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

            var report = await _reportService.GetProfitLossReportAsync(start, end, branchId, ct);
            return Ok(report);
        }

        [HttpGet("detailed-analysis")]
        public async Task<IActionResult> GetDetailedAnalysis(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] Guid? branchId,
            CancellationToken ct)
        {
            // 1. Current Period
            // Defaults to 'Last 30 Days' if not provided
            var currentStart = startDate?.Date ?? DateTime.UtcNow.Date.AddDays(-30);
            var currentEnd = endDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

            // 2. Previous Period calculation (Same duration immediately before currentStart)
            var duration = currentEnd - currentStart;
            // Ensure strictly distinct periods
            var previousEnd = currentStart.AddTicks(-1); 
            var previousStart = previousEnd.Subtract(duration);

            // 3. Fetch Data
            var currentPeriodData = await _reportService.GetProfitLossReportAsync(currentStart, currentEnd, branchId, ct);
            var previousPeriodData = await _reportService.GetProfitLossReportAsync(previousStart, previousEnd, branchId, ct);

            // 4. Calculate Changes
            var result = new DetailedReportResponse
            {
                CurrentPeriod = currentPeriodData,
                PreviousPeriod = previousPeriodData,
                Comparison = new ComparisonSummary
                {
                    RevenueChangePercentage = CalculateChange(currentPeriodData.TotalRevenue, previousPeriodData.TotalRevenue),
                    NetProfitChangePercentage = CalculateChange(currentPeriodData.TotalNetProfit, previousPeriodData.TotalNetProfit),
                    MarginChangePercentage = CalculateChange(currentPeriodData.TotalMarginPercentage, previousPeriodData.TotalMarginPercentage),
                    CostChangePercentage = CalculateChange(currentPeriodData.TotalInfoCost, previousPeriodData.TotalInfoCost)
                }
            };

            return Ok(result);
        }

        private double CalculateChange(decimal current, decimal previous)
        {
            if (previous == 0) return current > 0 ? 100 : 0;
            return (double)Math.Round(((current - previous) / previous) * 100, 2);
        }

        private double CalculateChange(double current, double previous)
        {
            if (previous == 0) return current > 0 ? 100 : 0;
            return Math.Round(((current - previous) / previous) * 100, 2);
        }
    }

    public class DetailedReportResponse
    {
        public ProfitLossReportDto CurrentPeriod { get; set; }
        public ProfitLossReportDto PreviousPeriod { get; set; }
        public ComparisonSummary Comparison { get; set; }
    }

    public class ComparisonSummary
    {
        public double RevenueChangePercentage { get; set; }
        public double NetProfitChangePercentage { get; set; }
        public double MarginChangePercentage { get; set; }
        public double CostChangePercentage { get; set; }
    }

}
