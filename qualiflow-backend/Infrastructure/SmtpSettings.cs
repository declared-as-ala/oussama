namespace DocApi.Infrastructure
{
    public class SmtpSettings
    {
        public bool Enabled { get; set; } = true;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public string Username { get; set; } = string.Empty;
        public string? UserName
        {
            get => Username;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    Username = value;
                }
            }
        }

        public string Password { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = "QualiFlow";
        public bool EnableSsl { get; set; } = true;
        public bool CheckCertificateRevocation { get; set; } = false;
    }
}
