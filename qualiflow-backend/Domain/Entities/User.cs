using System;

namespace DocApi.Domain.Entities
{
    public class User
    {
        public int Id { get; set; }
        public int? OrganizationId { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Email { get; set; }
        public string? Username { get; set; }
        public required string PasswordHash { get; set; }
        public required string Role { get; set; } // SUPER_ADMIN, ADMIN_ORG, etc.
        public string? Function { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public string? Nationality { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? PreferredLanguage { get; set; }
        public string? ProfilePhotoPath { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsEmailVerified { get; set; } = false;
        public string? EmailVerificationToken { get; set; }
        public DateTime? EmailVerificationExpiresAt { get; set; }
        public string? PendingEmail { get; set; }
        public string? EmailChangeVerificationToken { get; set; }
        public DateTime? EmailChangeVerificationExpiresAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
