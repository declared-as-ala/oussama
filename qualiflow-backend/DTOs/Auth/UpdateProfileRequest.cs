using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Auth
{
    public class UpdateProfileRequest
    {
        [Required]
        [MinLength(2)]
        public required string FirstName { get; set; }

        [Required]
        [MinLength(2)]
        public required string LastName { get; set; }

        public DateTime? BirthDate { get; set; }

        [MaxLength(30)]
        public string? Phone { get; set; }

        [MaxLength(120)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? Nationality { get; set; }

        [Required]
        [RegularExpression("^(fr|en|ar)$", ErrorMessage = "Supported languages are: fr, en, ar")]
        public required string PreferredLanguage { get; set; }
    }
}
