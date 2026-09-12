namespace RestaurantPos.Api.DTOs
{
    // Menü Mühendisliği - BCG Matrix Kategorileri
    public enum MenuEngineeringCategory
    {
        Star = 0,       // Yıldız: Yüksek Satış + Yüksek Kâr
        Plow = 1,       // At (Plow Horse): Yüksek Satış + Düşük Kâr
        Puzzle = 2,     // Bulmaca (Puzzle): Düşük Satış + Yüksek Kâr
        Dog = 3         // Köpek: Düşük Satış + Düşük Kâr
    }

    // Menü Mühendisliği DTO
    public class MenuEngineeringDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public int SalesVolume { get; set; }           // Satış Adedi
        public decimal ProfitMargin { get; set; }      // Kâr Marjı (%)
        public decimal Revenue { get; set; }           // Toplam Gelir
        public decimal TotalProfit { get; set; }       // Toplam Kâr
        public MenuEngineeringCategory Category { get; set; }
    }

    // Yoğunluk Haritası - Saatlik Veri
    public class HeatmapDataDto
    {
        public string DayOfWeek { get; set; }          // Pazartesi, Salı, etc.
        public int DayIndex { get; set; }              // 0 = Pazartesi, 6 = Pazar
        public int Hour { get; set; }                  // 0-23
        public decimal AverageRevenue { get; set; }    // O saatteki ortalama ciro
        public int AverageWaiters { get; set; }        // O saatteki ortalama garson sayısı
        public int OrderCount { get; set; }            // O saatteki sipariş sayısı
        public double Intensity { get; set; }          // 0-1 arası normalize edilmiş yoğunluk
    }

    // Genel Analytics Response
    public class AnalyticsResponseDto
    {
        public List<MenuEngineeringDto> MenuEngineering { get; set; } = new();
        public List<HeatmapDataDto> HeatmapData { get; set; } = new();
        
        // Özet İstatistikler
        public AnalyticsSummaryDto Summary { get; set; } = new();
    }

    // Özet İstatistikler
    public class AnalyticsSummaryDto
    {
        public decimal TotalRevenue { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal AverageProfitMargin { get; set; }
        public int TotalOrders { get; set; }
        public int TotalProductsSold { get; set; }
        public string BestSellingProduct { get; set; }
        public string MostProfitableProduct { get; set; }
        public string PeakHour { get; set; }           // En yoğun saat
        public string PeakDay { get; set; }            // En yoğun gün
    }
}
