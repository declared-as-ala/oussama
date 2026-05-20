using System;

namespace DocApi.Domain.Entities
{
    public class ActionLog
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public string Module { get; set; } = string.Empty; // e.g., "DOCUMENT", "PROCESS", "USER", "NC"
        public string ActionType { get; set; } = string.Empty; // e.g., "CREATE", "UPDATE", "DELETE", "LOGIN"
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int PerformedByUserId { get; set; }
        public string ActorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
