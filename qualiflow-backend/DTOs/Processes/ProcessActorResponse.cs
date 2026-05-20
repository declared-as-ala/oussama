using System;

namespace DocApi.DTOs.Processes
{
    public class ProcessActorResponse
    {
        public int UserId { get; set; }
        public required string FullName { get; set; }
        public required string Email { get; set; }
        public string? Function { get; set; }
        public required string ActorType { get; set; }
        public DateTime AssignedAt { get; set; }
    }
}
