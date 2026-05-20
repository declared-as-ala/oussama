using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Auth
{
    public class RequestEmailChangeCodeRequest
    {
        [Required]
        [EmailAddress]
        public required string NewEmail { get; set; }
    }
}
