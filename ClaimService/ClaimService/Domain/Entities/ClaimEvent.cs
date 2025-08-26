using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClaimService.Domain.Enums;

namespace ClaimService.Domain.Entities
{
    [Table("ClaimEvents")]
    public class ClaimEvent
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(20)]
        public string ClaimId { get; set; } = default!;

        [Required]
        public ClaimStatus FromStatus { get; set; }

        [Required]
        public ClaimStatus ToStatus { get; set; }

        [MaxLength(512)]
        public string? Remarks { get; set; }

        [Required]
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        [Required, MaxLength(64)]
        public string ActorUserId { get; set; } = default!;

        [Timestamp]
        public byte[] RowVersion { get; set; } = default!;
    }
}
