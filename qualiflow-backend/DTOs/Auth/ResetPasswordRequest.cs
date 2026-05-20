using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Auth
{
    public class ResetPasswordRequest
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [RegularExpression(@"^\d{6}$")]
        public required string Code { get; set; }

        [Required]
        [MinLength(8)]
        public required string NewPassword { get; set; }

        [Required]
        [MinLength(8)]
        public required string ConfirmPassword { get; set; }
    }
}
