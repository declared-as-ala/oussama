using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Notifications
{
    public sealed class RegisterWebPushSubscriptionRequest
    {
        [Required]
        [MaxLength(2000)]
        public required string Endpoint { get; set; }

        [Required]
        [MaxLength(512)]
        public required string P256dh { get; set; }

        [Required]
        [MaxLength(512)]
        public required string Auth { get; set; }
    }
}
