namespace AuthService.DTOs
{
    public class LoginRequest
    {
        public string LoginId { get; set; }  // Could be username or CNIC or personnel number
        public string Password { get; set; }
    }
}
