namespace DocApi.DTOs.CorrectiveActions
{
    public class UpdateCorrectiveActionStatusRequest
    {
        public required string Status { get; set; }
        public string? Comment { get; set; }
    }
}
