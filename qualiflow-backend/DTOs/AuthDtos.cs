using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs
{
    public class LoginRequest
    {
        [Required]
        public required string Username { get; set; }
        
        [Required]
        public required string Password { get; set; }
    }

    public class RegisterRequest
    {
        [Required]
        [MinLength(3)]
        public required string Username { get; set; }
        
        [Required]
        [EmailAddress]
        public required string Email { get; set; }
        
        [Required]
        [MinLength(6)]
        public required string Password { get; set; }
        
        public string Role { get; set; } = "User";
    }

    public class AuthResponse
    {
        public required string Token { get; set; }
        public required string Username { get; set; }
        public required string Role { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class UserResponse
    {
        public int Id { get; set; }
        public required string Username { get; set; }
        public required string Email { get; set; }
        public required string Role { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }
}