namespace DocApi.DTOs.Auth
{
    public class RegisterResponse
    {
        public int Id { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Email { get; set; }
        public required string Role { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool RequiresEmailVerification { get; set; }
        public bool VerificationEmailSent { get; set; } = true;
    }
}
