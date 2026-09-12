using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs.ExpenseInvoice
{
    public class CreateExpenseCategoryDto
    {
        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? NameAr { get; set; }

        [MaxLength(20)]
        public string? Color { get; set; }

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; }
    }

    public class UpdateExpenseCategoryDto : CreateExpenseCategoryDto { }

    public class ExpenseCategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        // Locale-resolved label — picked by the service via Accept-Language.
        public string DisplayName { get; set; } = string.Empty;
        public string? Color { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public int InvoiceCount { get; set; }
    }
}
