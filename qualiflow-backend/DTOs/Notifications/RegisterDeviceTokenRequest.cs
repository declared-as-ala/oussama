using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Notifications
{
    public class RegisterDeviceTokenRequest
    {
        [Required]
        [MinLength(20)]
        public string DeviceToken { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Platform { get; set; } = "unknown";

        [MaxLength(255)]
        public string? DeviceName { get; set; }
    }
}
