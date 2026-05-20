using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Users
{
    public class UpdateUserRequest
    {
        [Required]
        public required string FirstName { get; set; }

        [Required]
        public required string LastName { get; set; }

        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        public string? Function { get; set; }
    }
}
