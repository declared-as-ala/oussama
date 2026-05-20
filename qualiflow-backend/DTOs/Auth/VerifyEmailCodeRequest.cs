using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Auth
{
    public class VerifyEmailCodeRequest
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public required string Code { get; set; }
    }
}
