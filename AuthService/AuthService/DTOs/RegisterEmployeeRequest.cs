namespace AuthService.DTOs
{
    public class RegisterEmployeeRequest
    {
        public string CNIC { get; set; } // Required if no personnel number
        public string PersonnelNumber { get; set; } // Required if no CNIC
        public string Password { get; set; }
        public string FullName { get; set; }
        public string SystemCode { get; set; } // e.g., "PensionPortal"
    }
}
