namespace DocApi.DTOs.Notifications
{
    public class NotificationPreferenceItemRequest
    {
        public required string NotificationType { get; set; }
        public bool InAppEnabled { get; set; }
        public bool EmailEnabled { get; set; }
    }
}
