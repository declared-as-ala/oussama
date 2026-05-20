namespace DocApi.DTOs.NonConformities
{
    public class ValidateNonConformityRequest
    {
        public required string Code { get; set; }
        public int ResponsibleUserId { get; set; }
    }
}
