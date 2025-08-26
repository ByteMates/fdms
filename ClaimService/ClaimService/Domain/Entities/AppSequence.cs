using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClaimService.Domain.Entities;

[Table("AppSequences")]
public class AppSequence
{
    [Key, MaxLength(32)]
    public string Name { get; set; } = default!; // "ClaimId", "ClaimQueue"

    public long NextValue { get; set; }
}
