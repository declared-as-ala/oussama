using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Notifications
{
    public sealed class UnregisterWebPushSubscriptionRequest
    {
        [Required]
        [MaxLength(2000)]
        public required string Endpoint { get; set; }
    }
}
