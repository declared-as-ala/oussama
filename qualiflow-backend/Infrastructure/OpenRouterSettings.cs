namespace DocApi.Infrastructure
{
    public sealed class OpenRouterSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "baidu/cobuddy:free";
        public string ApiBaseUrl { get; set; } = "https://openrouter.ai/api/v1";
        public int TimeoutSeconds { get; set; } = 30;
        public double Temperature { get; set; } = 0.2;
        public int MaxOutputTokens { get; set; } = 2048;
    }
}
