using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Auth
{
    public class LoginByPhoneRequest
    {
        [Required]
        [Phone]
        public required string PhoneNumber { get; set; }

        [Required]
        [MinLength(6)]
        public required string Password { get; set; }
    }
}
