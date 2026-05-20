using System;

namespace DocApi.Domain.Entities
{
    public class DocumentExpirationPolicy
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int AlertDays30 { get; set; } = 30;
        public int AlertDays7 { get; set; } = 7;
        public int AlertDays1 { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
