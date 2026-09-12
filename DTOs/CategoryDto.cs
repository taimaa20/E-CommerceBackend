namespace RestaurantPos.Api.DTOs
{
    public class CategoryDto
    {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string? NameAr { get; set; }
            public string DisplayName { get; set; }
            public string? Description { get; set; }
            public string? ImageUrl { get; set; }
            public int SortOrder { get; set; }
            public int DisplayMode { get; set; }
            public bool IsActive { get; set; }
            public List<SubcategoryDto> Subcategories { get; set; } = new();
      
    }

    public class SubcategoryDto
    {
        public Guid Id { get; set; }
        public Guid CategoryId { get; set; }
        public string Name { get; set; }
        public string? NameAr { get; set; }
        public string DisplayName { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
    }

    public class SubcategoryCreateDto
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Subcategory name (EN) is required.")]
        [System.ComponentModel.DataAnnotations.MaxLength(100, ErrorMessage = "Subcategory name cannot exceed 100 characters.")]
        [System.ComponentModel.DataAnnotations.MinLength(1, ErrorMessage = "Subcategory name cannot be empty.")]
        public string Name { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(100, ErrorMessage = "Subcategory name (AR) cannot exceed 100 characters.")]
        public string? NameAr { get; set; }

        [System.ComponentModel.DataAnnotations.Range(0, 9999, ErrorMessage = "Display order must be between 0 and 9999.")]
        public int DisplayOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;
    }

    public class SubcategoryUpdateDto : SubcategoryCreateDto
    {
    }

    public class SubcategoryActiveDto
    {
        public bool IsActive { get; set; }
    }

    public class SubcategoryReorderDto
    {
        public Guid Id { get; set; }

        [System.ComponentModel.DataAnnotations.Range(0, 9999, ErrorMessage = "Display order must be between 0 and 9999.")]
        public int DisplayOrder { get; set; }
    }

    public class CategoryCreateDto
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Category name (EN) is required.")]
        [System.ComponentModel.DataAnnotations.MaxLength(100, ErrorMessage = "Category name cannot exceed 100 characters.")]
        [System.ComponentModel.DataAnnotations.MinLength(1, ErrorMessage = "Category name cannot be empty.")]
        public string Name { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(100, ErrorMessage = "Category name (AR) cannot exceed 100 characters.")]
        public string? NameAr { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(2000)]
        public string? Description { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(2000)]
        public string? ImageUrl { get; set; }

        [System.ComponentModel.DataAnnotations.Range(0, 9999, ErrorMessage = "Sort order must be between 0 and 9999.")]
        public int SortOrder { get; set; } = 0;

        [System.ComponentModel.DataAnnotations.Range(0, 1, ErrorMessage = "Display mode must be 0 (Grid) or 1 (List).")]
        public int DisplayMode { get; set; } = 0;

        public bool IsActive { get; set; } = true;
    }

    public class CategoryUpdateDto
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Category name (EN) is required.")]
        [System.ComponentModel.DataAnnotations.MaxLength(100, ErrorMessage = "Category name cannot exceed 100 characters.")]
        [System.ComponentModel.DataAnnotations.MinLength(1, ErrorMessage = "Category name cannot be empty.")]
        public string Name { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(100, ErrorMessage = "Category name (AR) cannot exceed 100 characters.")]
        public string? NameAr { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(2000)]
        public string? Description { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(2000)]
        public string? ImageUrl { get; set; }

        [System.ComponentModel.DataAnnotations.Range(0, 9999, ErrorMessage = "Sort order must be between 0 and 9999.")]
        public int SortOrder { get; set; }

        [System.ComponentModel.DataAnnotations.Range(0, 1, ErrorMessage = "Display mode must be 0 (Grid) or 1 (List).")]
        public int DisplayMode { get; set; }

        public bool IsActive { get; set; }
    }
}
