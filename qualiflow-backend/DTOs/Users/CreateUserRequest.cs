using System;
using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Users
{
    public class CreateUserRequest
    {
        [Required]
        public required string FirstName { get; set; }

        [Required]
        public required string LastName { get; set; }

        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [MinLength(6)]
        public required string Password { get; set; }

        public int? OrganizationId { get; set; }

        [Required]
        public required string Role { get; set; }

        public string? Function { get; set; }

        public string? Phone { get; set; }

        public string? City { get; set; }

        public DateTime? BirthDate { get; set; }
    }
}
