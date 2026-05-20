using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Organizations
{
    public class CreateOrganizationAdminRequest
    {
        [Required]
        [MinLength(2)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MinLength(2)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string TemporaryPassword { get; set; } = string.Empty;
    }
}
