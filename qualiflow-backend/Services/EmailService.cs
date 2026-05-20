using System.Threading.Tasks;
using DocApi.Infrastructure;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DocApi.Services
{
    public class EmailService : Interfaces.IEmailService
    {
        private readonly SmtpSettings _smtpSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<SmtpSettings> smtpSettings, ILogger<EmailService> logger)
        {
            _smtpSettings = smtpSettings.Value;
            _logger = logger;

            // Priority to environment variables (e.g. from .env)
            var envEnabled = Environment.GetEnvironmentVariable("SMTP_ENABLED");
            if (bool.TryParse(envEnabled, out var enabled)) _smtpSettings.Enabled = enabled;

            var envHost = Environment.GetEnvironmentVariable("SMTP_HOST");
            if (!string.IsNullOrEmpty(envHost)) _smtpSettings.Host = envHost;

            var envPort = Environment.GetEnvironmentVariable("SMTP_PORT");
            if (int.TryParse(envPort, out var port)) _smtpSettings.Port = port;

            var envUser = Environment.GetEnvironmentVariable("SMTP_USER");
            if (!string.IsNullOrEmpty(envUser)) _smtpSettings.Username = envUser;

            var envPass = Environment.GetEnvironmentVariable("SMTP_PASS");
            if (!string.IsNullOrEmpty(envPass)) _smtpSettings.Password = envPass;

            var envFromEmail = Environment.GetEnvironmentVariable("SMTP_SENDER_EMAIL");
            if (!string.IsNullOrEmpty(envFromEmail)) _smtpSettings.FromEmail = envFromEmail;

            var envFromName = Environment.GetEnvironmentVariable("SMTP_SENDER_NAME");
            if (!string.IsNullOrEmpty(envFromName)) _smtpSettings.FromName = envFromName;

            var envEnableSsl = Environment.GetEnvironmentVariable("SMTP_ENABLE_SSL");
            if (bool.TryParse(envEnableSsl, out var enableSsl))
            {
                _smtpSettings.EnableSsl = enableSsl;
            }
            else if (string.Equals(_smtpSettings.Host, "smtp.gmail.com", StringComparison.OrdinalIgnoreCase))
            {
                // Safe default for Gmail SMTP when the flag is omitted in local .env.
                _smtpSettings.EnableSsl = true;
            }

            var envCheckCertificateRevocation = Environment.GetEnvironmentVariable("SMTP_CHECK_CERTIFICATE_REVOCATION");
            if (bool.TryParse(envCheckCertificateRevocation, out var checkCertificateRevocation))
            {
                _smtpSettings.CheckCertificateRevocation = checkCertificateRevocation;
            }
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            if (!_smtpSettings.Enabled)
            {
                _logger.LogWarning(
                    "SMTP email disabled. Email to {Recipient} with subject {Subject} was skipped.",
                    to,
                    subject);
                return;
            }

            if (string.IsNullOrWhiteSpace(_smtpSettings.Host) ||
                string.IsNullOrWhiteSpace(_smtpSettings.Username) ||
                string.IsNullOrWhiteSpace(_smtpSettings.Password) ||
                string.IsNullOrWhiteSpace(_smtpSettings.FromEmail))
            {
                throw new InvalidOperationException("SMTP configuration is incomplete.");
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_smtpSettings.FromName, _smtpSettings.FromEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = body }.ToMessageBody();

            try
            {
                using var client = new SmtpClient
                {
                    CheckCertificateRevocation = _smtpSettings.CheckCertificateRevocation
                };

                await client.ConnectAsync(
                    _smtpSettings.Host,
                    _smtpSettings.Port,
                    GetSecureSocketOptions(),
                    cancellationToken: default);

                await client.AuthenticateAsync(_smtpSettings.Username, _smtpSettings.Password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation(
                    "Verification email sent to {Recipient} via {Host}:{Port} (SSL={EnableSsl}).",
                    to,
                    _smtpSettings.Host,
                    _smtpSettings.Port,
                    _smtpSettings.EnableSsl);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send email to {Recipient} via {Host}:{Port} (SSL={EnableSsl}, User={User}).",
                    to,
                    _smtpSettings.Host,
                    _smtpSettings.Port,
                    _smtpSettings.EnableSsl,
                    _smtpSettings.Username);
                throw;
            }
        }

        private SecureSocketOptions GetSecureSocketOptions()
        {
            if (!_smtpSettings.EnableSsl)
            {
                return SecureSocketOptions.None;
            }

            return _smtpSettings.Port == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;
        }
    }
}
