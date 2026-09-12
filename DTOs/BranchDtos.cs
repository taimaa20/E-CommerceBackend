using System.ComponentModel.DataAnnotations;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.DTOs
{
    public class BranchDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public bool IsMainBranch { get; set; }
        public bool IsActive { get; set; }
        public Guid? CreatedById { get; set; }
        public string? CreatedBy { get; set; }
        public Guid? UpdatedById { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class BranchCreateDto
    {
        [Required]
        [MaxLength(BranchDefaults.NameMaxLength)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(BranchDefaults.NameMaxLength)]
        public string? NameAr { get; set; }

        [Required]
        [MaxLength(BranchDefaults.CodeMaxLength)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(500)]
        public string? Address { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [EmailAddress]
        [MaxLength(200)]
        public string? Email { get; set; }

        public bool IsMainBranch { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class BranchUpdateDto
    {
        [Required]
        [MaxLength(BranchDefaults.NameMaxLength)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(BranchDefaults.NameMaxLength)]
        public string? NameAr { get; set; }

        [Required]
        [MaxLength(BranchDefaults.CodeMaxLength)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(500)]
        public string? Address { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [EmailAddress]
        [MaxLength(200)]
        public string? Email { get; set; }

        public bool IsMainBranch { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class BranchPagedResultDto
    {
        public IReadOnlyList<BranchDto> Items { get; set; } = new List<BranchDto>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}
