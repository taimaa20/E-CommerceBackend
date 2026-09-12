using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.DTOs
{
    public class UserBranchAssignmentDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string? BranchNameAr { get; set; }
        public string BranchCode { get; set; } = string.Empty;
        public bool BranchIsActive { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UserBranchAssignmentsDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? FullNameAr { get; set; }
        public IReadOnlyList<UserBranchAssignmentDto> Assignments { get; set; } = Array.Empty<UserBranchAssignmentDto>();
    }

    public class UserBranchAssignDto
    {
        [Required]
        public Guid BranchId { get; set; }

        public bool IsDefault { get; set; }
    }

    public class UserBranchRemoveDto
    {
        public Guid? NewDefaultBranchId { get; set; }
    }
}
