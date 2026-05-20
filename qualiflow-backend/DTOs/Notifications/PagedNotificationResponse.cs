using System.Collections.Generic;

namespace DocApi.DTOs.Notifications
{
    public class PagedNotificationResponse
    {
        public int Total { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public List<NotificationListItemResponse> Items { get; set; } = new();
    }
}
