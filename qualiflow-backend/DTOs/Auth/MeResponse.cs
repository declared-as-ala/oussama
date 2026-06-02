namespace DocApi.DTOs.Auth
{
    public class MeResponse
    {
        public int Id { get; set; }
        public int? OrganizationId { get; set; }
        public string? OrganizationName { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Email { get; set; }
        public required string Role { get; set; }
        public string? Function { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public string? Nationality { get; set; }
        public DateTime? BirthDate { get; set; }
        public string PreferredLanguage { get; set; } = "fr";
        public string? ProfilePhotoPath { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
