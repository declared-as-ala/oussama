using System;

namespace DocApi.Domain.Entities
{
    public class ProcessActor
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int ProcessId { get; set; }
        public int UserId { get; set; }
        public required string ActorType { get; set; }
        public DateTime AssignedAt { get; set; }
    }
}
