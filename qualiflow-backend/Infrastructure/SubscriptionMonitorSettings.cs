namespace DocApi.Infrastructure
{
    public sealed class SubscriptionMonitorSettings
    {
        public bool Enabled { get; set; } = true;
        public int PollingIntervalMinutes { get; set; } = 60;
    }
}
