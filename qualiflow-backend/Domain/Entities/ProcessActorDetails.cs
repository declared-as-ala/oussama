using System;

namespace DocApi.Domain.Entities
{
    public class ProcessActorDetails
    {
        public int UserId { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Email { get; set; }
        public string? Function { get; set; }
        public required string ActorType { get; set; }
        public DateTime AssignedAt { get; set; }
    }
}
