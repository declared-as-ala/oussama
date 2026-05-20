using System.Collections.Generic;

namespace DocApi.DTOs.NonConformities
{
    public class NonConformityDetailsResponse
    {
        public required NonConformityResponse NonConformity { get; set; }
        public List<CorrectiveActionResponse> Actions { get; set; } = new();
        public List<NonConformityAttachmentResponse> Attachments { get; set; } = new();
    }
}
