using System.Collections.Generic;

namespace DocApi.DTOs.Processes
{
    public class ProcessMapResponse
    {
        public List<ProcessListItemResponse> PilotageProcesses { get; set; } = new();
        public List<ProcessListItemResponse> RealisationProcesses { get; set; } = new();
        public List<ProcessListItemResponse> SupportProcesses { get; set; } = new();
    }
}
