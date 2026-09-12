using System.ComponentModel.DataAnnotations;

namespace RestaurantPos.Api.Models
{
    public class UserBranch : BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public bool IsDefault { get; set; }

        public Guid? CreatedById { get; set; }

        [MaxLength(150)]
        public string? CreatedBy { get; set; }

        public Guid? UpdatedById { get; set; }

        [MaxLength(150)]
        public string? UpdatedBy { get; set; }
    }
}
