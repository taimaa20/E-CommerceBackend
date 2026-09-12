using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs
{
    /// <summary>Input DTO for creating a customer. Prevents mass-assignment of TenantId,
    /// LoyaltyPoints, Tier, CreatedAt, etc. (overposting risk on the raw Customer entity).</summary>
    public class CustomerCreateDto
    {
        [Required, StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        public string? NameAr { get; set; }

        [Required, StringLength(20, MinimumLength = 3), Phone]
        public string PhoneNumber { get; set; } = string.Empty;

        [EmailAddress, StringLength(150)]
        public string? Email { get; set; }

        public DateTime? BirthDate { get; set; }
    }

    /// <summary>Output DTO returned by /search and POST /customers. Field names
    /// match the previous anonymous-object response 1:1 so frontends are unaffected.</summary>
    public class CustomerDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public DateTime? BirthDate { get; set; }
        public CustomerTier Tier { get; set; }
        public decimal LoyaltyPoints { get; set; }
        public DateTime LastVisit { get; set; }
    }
}
