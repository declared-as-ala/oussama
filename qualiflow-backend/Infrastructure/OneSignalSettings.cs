namespace DocApi.Infrastructure
{
    public sealed class OneSignalSettings
    {
        public bool Enabled { get; set; } = false;
        public string AppId { get; set; } = string.Empty;
        public string RestApiKey { get; set; } = string.Empty;
        public string ApiBaseUrl { get; set; } = "https://api.onesignal.com";
        public string DefaultLanguage { get; set; } = "en";
    }
}
