using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.DTOs.Support;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocApi.Services
{
    public sealed class SupportService : ISupportService
    {
        private readonly SupportAssistantSettings _supportSettings;
        private readonly IUserRepository _userRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IEmailService _emailService;
        private readonly ILogger<SupportService> _logger;

        public SupportService(
            IOptions<SupportAssistantSettings> supportSettings,
            IUserRepository userRepository,
            INotificationRepository notificationRepository,
            IEmailService emailService,
            ILogger<SupportService> logger)
        {
            _supportSettings = supportSettings.Value;
            _userRepository = userRepository;
            _notificationRepository = notificationRepository;
            _emailService = emailService;
            _logger = logger;
        }

        public Task<SupportContactInfoResponse> GetContactInfoAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SupportContactInfoResponse
            {
                AssistantName = _supportSettings.Name,
                AssistantEmail = _supportSettings.Email,
                AssistantPhone = _supportSettings.Phone
            });
        }

        public async Task<SubmitSupportTicketResponse> SubmitTicketAsync(
            SubmitSupportTicketRequest request,
            UserContext userContext,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var organizationName = request.OrganizationName.Trim();
            var problemType = request.ProblemType.Trim();
            var description = request.Description.Trim();

            if (string.IsNullOrWhiteSpace(normalizedEmail) ||
                string.IsNullOrWhiteSpace(organizationName) ||
                string.IsNullOrWhiteSpace(problemType) ||
                string.IsNullOrWhiteSpace(description))
            {
                throw new ServiceException("Tous les champs du ticket support sont obligatoires.");
            }

            var users = await _userRepository.GetAllAsync();
            var superAdmins = users
                .Where(u => u.IsActive && string.Equals(u.Role, UserRoles.SUPER_ADMIN, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (superAdmins.Count == 0)
            {
                throw new ServiceException("Aucun super administrateur actif n'est disponible.");
            }

            var title = $"Ticket support: {problemType}";
            var message = BuildNotificationMessage(organizationName, normalizedEmail, problemType, description);

            foreach (var admin in superAdmins)
            {
                var notification = new Notification
                {
                    OrganizationId = admin.OrganizationId,
                    UserId = admin.Id,
                    SenderId = userContext.UserId,
                    Type = NotificationConstants.TypeSystemAlert,
                    Category = NotificationConstants.CategoryWarning,
                    Title = title,
                    Message = message,
                    Priority = NotificationConstants.PriorityHigh,
                    IsRead = false,
                    IsPushSent = false,
                    Channel = "INAPP",
                    IsArchived = false,
                    ReferenceType = "SUPPORT_TICKET",
                    ReferenceId = null,
                    ActionUrl = "/notifications",
                    CreatedAt = DateTime.UtcNow
                };

                await _notificationRepository.CreateAsync(notification);

                try
                {
                    await _emailService.SendEmailAsync(
                        admin.Email,
                        $"[Support GED] {problemType} - {organizationName}",
                        BuildEmailBody(organizationName, normalizedEmail, problemType, description, userContext));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Echec envoi email support vers super admin {Email}", admin.Email);
                }
            }

            return new SubmitSupportTicketResponse
            {
                Success = true,
                Message = "Votre ticket support a ete envoye au super administrateur."
            };
        }

        private static string BuildNotificationMessage(string organizationName, string email, string problemType, string description)
        {
            var shortDescription = description.Length <= 350 ? description : $"{description[..350]}...";
            return $"Organisation: {organizationName}\nEmail: {email}\nType probleme: {problemType}\nDescription: {shortDescription}";
        }

        private static string BuildEmailBody(
            string organizationName,
            string email,
            string problemType,
            string description,
            UserContext userContext)
        {
            var builder = new StringBuilder();
            builder.AppendLine("<h2>Nouveau ticket support GED</h2>");
            builder.AppendLine("<p>Un utilisateur a declare un probleme depuis l'interface Parametres.</p>");
            builder.AppendLine("<ul>");
            builder.AppendLine($"<li><strong>Organisation:</strong> {System.Net.WebUtility.HtmlEncode(organizationName)}</li>");
            builder.AppendLine($"<li><strong>Email contact:</strong> {System.Net.WebUtility.HtmlEncode(email)}</li>");
            builder.AppendLine($"<li><strong>Type de probleme:</strong> {System.Net.WebUtility.HtmlEncode(problemType)}</li>");
            builder.AppendLine($"<li><strong>UserId emetteur:</strong> {userContext.UserId}</li>");
            builder.AppendLine("</ul>");
            builder.AppendLine("<p><strong>Description:</strong></p>");
            builder.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(description).Replace("\n", "<br/>")}</p>");
            return builder.ToString();
        }
    }
}
