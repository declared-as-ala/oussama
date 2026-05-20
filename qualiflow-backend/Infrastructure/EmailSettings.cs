namespace DocApi.Infrastructure
{
    public class EmailSettings
    {
        public required string Host { get; set; }
        public int Port { get; set; }
        public required string UserName { get; set; }
        public required string Password { get; set; }
        public bool EnableSsl { get; set; }
        public required string FromEmail { get; set; }
        public required string FromName { get; set; }
    }
}
