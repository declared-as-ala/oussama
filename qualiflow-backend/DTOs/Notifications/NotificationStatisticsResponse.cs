using System.Collections.Generic;

namespace DocApi.DTOs.Notifications
{
    public class NotificationStatisticsResponse
    {
        public int Total { get; set; }
        public int Unread { get; set; }
        public int Read { get; set; }
        public int Archived { get; set; }
        public int Critical { get; set; }
        public int High { get; set; }
        public Dictionary<string, int> ByCategory { get; set; } = new();
        public Dictionary<string, int> ByType { get; set; } = new();
    }
}
