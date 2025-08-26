using ClaimService.Domain.Enums;

namespace ClaimService.Infrastructure.Helpers
{
    public static class RoleNames
    {
        public static string Get(ClaimRoles role)
        {
            return role switch
            {
                ClaimRoles.MedicalAdmin => "MedicalClaims:Admin",
                ClaimRoles.MedicalSMB => "MedicalClaims:SMB",
                ClaimRoles.MedicalHospital => "MedicalClaims:Hospital",
                ClaimRoles.MedicalReviewer => "MedicalClaims:Reviewer",
                ClaimRoles.MedicalSO => "MedicalClaims:somedical",
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
            };
        }
    }
}
