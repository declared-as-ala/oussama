using System.Collections.Generic;

namespace DocApi.DTOs.Documents
{
    public class DocumentStatisticsResponse
    {
        public int Total { get; set; }
        public int Approved { get; set; }
        public int InReview { get; set; }
        public int Expired { get; set; }
        public int Draft { get; set; }
        public int Archived { get; set; }
        public int RecentlyUpdated { get; set; }
        public Dictionary<string, int> ByType { get; set; } = new();
        public Dictionary<string, int> ByProcess { get; set; } = new();
    }
}
