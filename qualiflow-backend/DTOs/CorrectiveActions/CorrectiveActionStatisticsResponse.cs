using System.Collections.Generic;

namespace DocApi.DTOs.CorrectiveActions
{
    public class CorrectiveActionStatisticsResponse
    {
        public int Total { get; set; }
        public int Planned { get; set; }
        public int InProgress { get; set; }
        public int Completed { get; set; }
        public int Verified { get; set; }
        public int Overdue { get; set; }
        public Dictionary<string, int> ByType { get; set; } = new();
        public Dictionary<string, int> ByResponsible { get; set; } = new();
        public Dictionary<string, int> ByNonConformity { get; set; } = new();
    }
}
