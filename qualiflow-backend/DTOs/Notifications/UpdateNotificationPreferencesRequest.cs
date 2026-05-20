using System.Collections.Generic;

namespace DocApi.DTOs.Notifications
{
    public class UpdateNotificationPreferencesRequest
    {
        public List<NotificationPreferenceItemRequest> Items { get; set; } = new();
    }
}
