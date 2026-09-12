namespace RestaurantPos.Api.DTOs
{
    public class DailySummaryDto
    {
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public int TotalCashOrders { get; set; }
        public int TotalOnlineOrders { get; set; }
        public int TotalOrdersCreated { get; set; }
        public int CancelledOrders { get; set; }
        public decimal CancellationRate { get; set; }
        public int CancelWasteCount { get; set; }
        public int ExpiryWasteCount { get; set; }
        public int ActiveTables { get; set; }
        public List<BestSellingProductDto> BestSellingProducts { get; set; } = new();
        public List<HourlySalesDto> HourlySales { get; set; } = new();
        public List<RecentOrderDto> RecentOrders { get; set; } = new();
        public List<PaymentMethodBreakdownDto> PaymentMethodBreakdown { get; set; } = new();
    }

    public class HourlySalesDto
    {
        public string Hour { get; set; } = string.Empty;
        public decimal Sales { get; set; }
    }

    public class RecentOrderDto
    {
        public string Id { get; set; } = string.Empty;
        public string Table { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class BestSellingProductDto
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Revenue { get; set; }
    }

    public class DailyReportDto
    {
        public DateTime Date { get; set; }
        public int TotalOrders { get; set; }
        public decimal CashPayments { get; set; }
        public decimal CardPayments { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<PaymentMethodBreakdownDto> PaymentMethodBreakdown { get; set; } = new();
    }

    public class PaymentMethodBreakdownDto
    {
        public string Method { get; set; } = string.Empty;
        public int Orders { get; set; }
        public decimal Amount { get; set; }
    }
}
