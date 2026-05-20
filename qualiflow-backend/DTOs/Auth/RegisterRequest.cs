using System;
using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Auth
{
    public class RegisterRequest
    {
        [Required]
        [MinLength(2)]
        public required string FirstName { get; set; }

        [Required]
        [MinLength(2)]
        public required string LastName { get; set; }

        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [MinLength(2)]
        [MaxLength(50)]
        public required string OrganizationCode { get; set; }

        [Required]
        public DateTime BirthDate { get; set; }

        [Required]
        [MinLength(8)]
        public required string Password { get; set; }

        [Required]
        [MinLength(8)]
        public required string ConfirmPassword { get; set; }

        public int CaptchaNum1 { get; set; }
        public int CaptchaNum2 { get; set; }
        public int CaptchaAnswer { get; set; }
    }
}
