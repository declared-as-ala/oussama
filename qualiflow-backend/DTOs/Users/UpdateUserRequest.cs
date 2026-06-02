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

        public string? Phone { get; set; }

        public string? City { get; set; }

        public string? Nationality { get; set; }

        public DateTime? BirthDate { get; set; }
    }
}
