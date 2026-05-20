using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Auth
{
    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }
    }
}
