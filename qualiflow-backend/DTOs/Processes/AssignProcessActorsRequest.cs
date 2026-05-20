using System.Collections.Generic;

namespace DocApi.DTOs.Processes
{
    public class AssignProcessActorsRequest
    {
        public List<AssignProcessActorItemRequest> Actors { get; set; } = new();
    }

    public class AssignProcessActorItemRequest
    {
        public int UserId { get; set; }
        public required string ActorType { get; set; }
    }
}
