using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Auth
{
    public class RefreshTokenRequest
    {
        [Required]
        public required string RefreshToken { get; set; }
    }
}
