using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using ClaimService.Domain.Enums;

namespace ClaimService.Domain.Entities
{
    [Table("Claims")]
    public class Claim
    {
        [Key, MaxLength(20)]
        public string ClaimId { get; set; } = default!;   // e.g., CLM-000001

        [Required, MaxLength(64)]
        public string EmployeeId { get; set; } = default!; // from EmployeeService

        [Required]
        public ClaimType ClaimType { get; set; }

        [Required]
        public DateTime ClaimDateUtc { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountClaimed { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? AmountApproved { get; set; }

        [MaxLength(32)]
        public string? HospitalCode { get; set; }

        [Required]
        public ClaimStatus Status { get; set; } = ClaimStatus.Draft;

        public long? QueueNo { get; set; } // set on submit

        [Required, MaxLength(64)]
        public string CreatedByUserId { get; set; } = default!;

        [Required]
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;

        [Timestamp]
        public byte[] RowVersion { get; set; } = default!;

        public ICollection<ClaimEvent> Events { get; set; } = new List<ClaimEvent>();
    }
}
