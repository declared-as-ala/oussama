using System.Collections.Generic;

namespace DocApi.DTOs.Processes
{
    public class ProcessStatisticsResponse
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public int Inactive { get; set; }
        public Dictionary<string, int> ByType { get; set; } = new();
        public int WithPilot { get; set; }
        public int WithoutPilot { get; set; }
    }
}
