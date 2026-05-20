using System;

namespace DocApi.DTOs.Organizations
{
    public class OrganizationLogoResponse
    {
        public int OrganizationId { get; set; }
        public string? LogoPath { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
