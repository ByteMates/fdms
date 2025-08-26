using Microsoft.AspNetCore.Identity;

namespace AuthService.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public string? CNIC { get; set; }
        public string? PersonnelNumber { get; set; }
        public string AuthProvider { get; set; } // "Local", "Google", "Facebook"

        // Add this navigation property
        public ICollection<UserSystemRole> UserSystemRoles { get; set; }

    }
}
