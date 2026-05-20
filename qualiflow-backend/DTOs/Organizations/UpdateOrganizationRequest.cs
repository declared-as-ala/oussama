using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Organizations
{
    public class UpdateOrganizationRequest
    {
        [Required]
        [MinLength(2)]
        public required string Name { get; set; }

        public string? Description { get; set; }

        [Required]
        public string Type { get; set; } = "INSTITUT";

        public string? Address { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        public string? Phone { get; set; }

        [RegularExpression("^(ACTIF|SUSPENDUE)$", ErrorMessage = "Status must be ACTIF or SUSPENDUE")]
        public string Status { get; set; } = "ACTIF";

        [Range(0, 3650)]
        public int? SubscriptionDaysRemaining { get; set; }

        public bool? SubscriptionMonitorEnabled { get; set; }
    }
}
