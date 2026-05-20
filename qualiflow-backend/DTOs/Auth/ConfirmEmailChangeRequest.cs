using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Auth
{
    public class ConfirmEmailChangeRequest
    {
        [Required]
        [EmailAddress]
        public required string NewEmail { get; set; }

        [Required]
        [MinLength(6)]
        [MaxLength(6)]
        public required string Code { get; set; }
    }
}
