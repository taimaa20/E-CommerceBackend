using System;
using System.Collections.Generic;

namespace RestaurantPos.Api.DTOs
{
    public class ProfitLossReportDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        
        public List<ProductProfitabilityDto> ProductPerformance { get; set; } = new();
        public List<DailyProfitLossDto> DailyStats { get; set; } = new();

        // Genel Toplamlar
        public decimal TotalRevenue { get; set; }
        public decimal TotalInfoCost { get; set; } // COGS
        public decimal TotalLaborCost { get; set; } // HR Cost
        public decimal TotalNetProfit { get; set; }
        public decimal TotalMarginPercentage { get; set; }
    }

    public class ProductProfitabilityDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        
        public int TotalSalesCount { get; set; }
        public decimal TotalRevenue { get; set; } // Brüt Ciro
        
        public decimal UnitCost { get; set; } // Birim Maliyet (Güncel Reçete)
        public decimal TotalCost { get; set; } // Toplam Maliyet (Satış Adedi * Birim Maliyet)
        
        public decimal NetProfit { get; set; }
        public decimal MarginPercentage { get; set; } // % Kâr Marjı
    }

    public class DailyProfitLossDto
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal NetProfit { get; set; }
        public decimal Margin { get; set; }
        public decimal LaborCost { get; set; } // Daily Labor Cost
    }
}
