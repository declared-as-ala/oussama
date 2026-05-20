using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Auth
{
    public class ChangePasswordRequest
    {
        [Required]
        [MinLength(6)]
        public required string CurrentPassword { get; set; }

        [Required]
        [MinLength(8)]
        public required string NewPassword { get; set; }

        [Required]
        [MinLength(8)]
        public required string ConfirmPassword { get; set; }
    }
}
