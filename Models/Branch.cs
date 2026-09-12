using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.Models
{
    public static class BranchDefaults
    {
        public static readonly Guid MainBranchId = Guid.Parse("0d5d315d-4bb4-4dc3-9ac8-c6ef4fcf0f01");
        public const string MainBranchName = "Main Branch";
        public const string MainBranchNameAr = "الفرع الرئيسي";
        public const string MainBranchCode = "MAIN";
        public const int CodeMaxLength = 16;
        public const int NameMaxLength = 120;
    }

    public class Branch : BaseEntity
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

        [MaxLength(200)]
        public string? Email { get; set; }

        public bool IsMainBranch { get; set; }

        public bool IsActive { get; set; } = true;

        public Guid? CreatedById { get; set; }

        [MaxLength(150)]
        public string? CreatedBy { get; set; }

        public Guid? UpdatedById { get; set; }

        [MaxLength(150)]
        public string? UpdatedBy { get; set; }
    }
}
