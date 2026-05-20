using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Support
{
    public class SupportContactInfoResponse
    {
        public string AssistantName { get; set; } = string.Empty;
        public string AssistantEmail { get; set; } = string.Empty;
        public string AssistantPhone { get; set; } = string.Empty;
    }

    public class SubmitSupportTicketRequest
    {
        [Required]
        [EmailAddress]
        [MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string OrganizationName { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string ProblemType { get; set; } = string.Empty;

        [Required]
        [MaxLength(4000)]
        public string Description { get; set; } = string.Empty;
    }

    public class SubmitSupportTicketResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
