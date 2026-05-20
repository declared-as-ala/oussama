namespace DocApi.Services.Models
{
    public sealed class OneSignalSendResult
    {
        public bool IsSuccess { get; set; }
        public string? NotificationId { get; set; }
        public string? Error { get; set; }
    }
}
