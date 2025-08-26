namespace AuthService.DTOs
{
    public class RegisterRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public string SystemCode { get; set; } // e.g., HRMIS, MedicalClaims
    }
}
