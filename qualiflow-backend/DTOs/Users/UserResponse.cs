namespace DocApi.DTOs.Users
{
    public class UserResponse
    {
        public int Id { get; set; }
        public int? OrganizationId { get; set; }
        public string? OrganizationName { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Email { get; set; }
        public required string Role { get; set; }
        public string? Function { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
