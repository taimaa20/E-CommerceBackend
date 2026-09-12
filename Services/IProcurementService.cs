using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services
{
    public interface IProcurementService
    {
        Task<List<SuggestedOrderDto>> GetSuggestedOrdersAsync();
    }

    public class SuggestedOrderDto
    {
        public Guid RawMaterialId { get; set; }
        public string RawMaterialName { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal MinimumStockLevel { get; set; }
        public decimal SuggestedQuantity { get; set; }
        public string Unit { get; set; }
        public decimal EstimatedCost { get; set; }
    }
}
