using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Users
{
    public class ChangeUserRoleRequest
    {
        [Required]
        public required string Role { get; set; }
    }
}
