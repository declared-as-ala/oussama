using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Auth
{
    public class ResendVerificationCodeRequest
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }
    }
}
