using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Notifications
{
    public class NotificationRecipientsQueryRequest
    {
        [Required]
        [MaxLength(60)]
        public required string EventType { get; set; }

        public int? DocumentId { get; set; }
    }
}
