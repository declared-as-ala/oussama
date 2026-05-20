using System.Collections.Generic;

namespace DocApi.DTOs.NonConformities
{
    public class NonConformityStatisticsResponse
    {
        public int Total { get; set; }
        public int PendingValidation { get; set; }
        public int Opened { get; set; }
        public int InProgress { get; set; }
        public int Closed { get; set; }
        public int Critical { get; set; }
        public int OverdueActions { get; set; }
        public Dictionary<string, int> BySeverity { get; set; } = new();
        public Dictionary<string, int> ByStatus { get; set; } = new();
    }
}
