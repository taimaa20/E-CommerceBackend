using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.Models
{
    public enum IdempotencyEntryState
    {
        Processing = 0,
        Completed = 1
    }

    public class IdempotencyEntry
    {
        [Key]
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }

        [Required]
        [MaxLength(120)]
        public string Key { get; set; } = string.Empty;

        [Required]
        [MaxLength(16)]
        public string Method { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Path { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string RequestHash { get; set; } = string.Empty;

        public IdempotencyEntryState State { get; set; } = IdempotencyEntryState.Processing;

        public int? StatusCode { get; set; }

        public string? ContentType { get; set; }

        [Column(TypeName = "text")]
        public string? ResponseBody { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        public DateTime ProcessingExpiresAt { get; set; }
    }
}
