namespace DocApi.Infrastructure
{
    public sealed class FirebaseSettings
    {
        public bool Enabled { get; set; } = false;
        public string ServiceAccountPath { get; set; } = string.Empty;
        public string ServiceAccountJson { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
    }
}
