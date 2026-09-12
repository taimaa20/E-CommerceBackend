using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs
{
    public class BranchContextBranchDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string Code { get; set; } = string.Empty;
        public bool IsMainBranch { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
        public bool IsCurrent { get; set; }
    }

    public class BranchContextDto
    {
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public BranchContextBranchDto CurrentBranch { get; set; } = new();
        public IReadOnlyList<BranchContextBranchDto> AssignedBranches { get; set; } = Array.Empty<BranchContextBranchDto>();
        public bool RequiresBranchSelector { get; set; }
    }

    public class BranchSwitchDto
    {
        [Required]
        public Guid BranchId { get; set; }
    }
}
