namespace DocApi.DTOs.Notifications
{
    public class NotificationPreferenceResponse
    {
        public required string NotificationType { get; set; }
        public bool InAppEnabled { get; set; }
        public bool EmailEnabled { get; set; }
    }
}
