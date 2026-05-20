using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Organizations
{
    public class ToggleOrganizationStatusRequest
    {
        [RegularExpression("^(ACTIF|SUSPENDUE)$", ErrorMessage = "Status must be ACTIF or SUSPENDUE")]
        public string? Status { get; set; }
    }
}
