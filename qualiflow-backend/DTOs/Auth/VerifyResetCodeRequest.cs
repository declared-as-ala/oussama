using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Auth
{
    public class VerifyResetCodeRequest
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [RegularExpression(@"^\d{6}$")]
        public required string Code { get; set; }
    }
}
