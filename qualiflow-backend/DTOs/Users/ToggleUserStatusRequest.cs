using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Users
{
    public class ToggleUserStatusRequest
    {
        [Required]
        public bool IsActive { get; set; }
    }
}
