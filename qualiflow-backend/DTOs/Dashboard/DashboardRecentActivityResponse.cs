using System;

namespace DocApi.DTOs.Dashboard
{
    public class DashboardRecentActivityResponse
    {
        public string Type { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? ActorName { get; set; }
    }
}
