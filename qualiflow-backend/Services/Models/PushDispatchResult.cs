namespace DocApi.Services.Models
{
    public sealed class PushDispatchResult
    {
        public bool IsSent { get; set; }
        public string Channel { get; set; } = "PUSH";
        public string? ExternalProviderId { get; set; }
    }
}
