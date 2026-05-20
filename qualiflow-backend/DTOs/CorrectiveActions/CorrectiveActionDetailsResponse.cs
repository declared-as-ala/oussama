using System.Collections.Generic;

namespace DocApi.DTOs.CorrectiveActions
{
    public class CorrectiveActionDetailsResponse
    {
        public required CorrectiveActionResponse Action { get; set; }
        public required CorrectiveActionLinkedNonConformityResponse NonConformity { get; set; }
        public required CorrectiveActionResponsibleResponse Responsible { get; set; }
        public CorrectiveActionProofRecordResponse? Proof { get; set; }
        public List<CorrectiveActionActionLogResponse> History { get; set; } = new();
    }

    public class CorrectiveActionLinkedNonConformityResponse
    {
        public int Id { get; set; }
        public string? Code { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
    }

    public class CorrectiveActionResponsibleResponse
    {
        public int Id { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
    }

    public class CorrectiveActionProofRecordResponse
    {
        public int Id { get; set; }
        public string? Code { get; set; }
        public string? Title { get; set; }
        public string? Type { get; set; }
    }
}
