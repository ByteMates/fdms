using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthService.Models
{
    public class UserSystemRole
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; }
        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }

        public string SystemCode { get; set; } // e.g., "HRMIS", "MedicalClaims"
        public string RoleName { get; set; }   // e.g., "Admin", "Employee"
        public bool IsActive { get; set; } = true;
    }
}
