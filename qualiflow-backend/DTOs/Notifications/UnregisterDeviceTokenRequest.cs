using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Notifications
{
    public class UnregisterDeviceTokenRequest
    {
        [Required]
        [MinLength(20)]
        public string DeviceToken { get; set; } = string.Empty;
    }
}
