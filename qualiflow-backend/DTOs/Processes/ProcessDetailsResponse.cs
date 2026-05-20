using System.Collections.Generic;

namespace DocApi.DTOs.Processes
{
    public class ProcessDetailsResponse
    {
        public required ProcessResponse Process { get; set; }
        public List<ProcessActorResponse> Actors { get; set; } = new();
    }
}
