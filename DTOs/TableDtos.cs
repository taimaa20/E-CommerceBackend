namespace RestaurantPos.Api.DTOs
{
    public class UpdateTableRequest
    {
        public string? Name { get; set; }
        public string? NameAr { get; set; }
        public int Capacity { get; set; }
        public Guid? TableCategoryId { get; set; }
    }

    public class TableCategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? Color { get; set; }
        public int TableCount { get; set; }
    }

    public class CreateTableCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string? Color { get; set; }
    }

    // Result outcomes — keep wire-format mapping in the controller without throwing
    // for expected business outcomes (duplicate name, missing id).
    public record TableCategoryCreateOutcome(TableCategoryDto? Dto, bool DuplicateName);
    public record TableCategoryUpdateOutcome(TableCategoryDto? Dto, bool NotFound, bool DuplicateName);
}
