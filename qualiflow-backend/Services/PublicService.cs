using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.DTOs.Public;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;
using Microsoft.Extensions.Logging;

using Microsoft.Extensions.Caching.Memory;

namespace DocApi.Services
{
    public sealed class PublicService : IPublicService
    {
        private readonly IUserRepository _userRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IEmailService _emailService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PublicService> _logger;

        public PublicService(
            IUserRepository userRepository,
            INotificationRepository notificationRepository,
            IEmailService emailService,
            IMemoryCache cache,
            ILogger<PublicService> logger)
        {
            _userRepository = userRepository;
            _notificationRepository = notificationRepository;
            _emailService = emailService;
            _cache = cache;
            _logger = logger;
        }

        public async Task<SubmitOrganizationRequestResponse> SendVerificationCodeAsync(string email, CancellationToken cancellationToken = default)
        {
            var code = new Random().Next(100000, 999999).ToString();
            var cacheKey = $"OrgRequestCode_{email.ToLowerInvariant()}";
            
            _cache.Set(cacheKey, code, TimeSpan.FromMinutes(10));

            try
            {
                await _emailService.SendEmailAsync(
                    email,
                    "[QualiFlow] Votre code de validation",
                    $"<div style='font-family: Arial, sans-serif; text-align: center; padding: 20px;'>" +
                    $"<h2 style='color: #064e3b;'>Validation de votre demande</h2>" +
                    $"<p>Veuillez utiliser le code suivant pour valider votre demande d'organisation :</p>" +
                    $"<div style='font-size: 32px; font-weight: bold; color: #10b981; margin: 20px 0; letter-spacing: 5px;'>{code}</div>" +
                    $"<p style='font-size: 12px; color: #64748b;'>Ce code expirera dans 10 minutes.</p>" +
                    $"</div>");

                return new SubmitOrganizationRequestResponse { Success = true, Message = "Code envoyé avec succès." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi du code de validation à {Email}", email);
                return new SubmitOrganizationRequestResponse { Success = false, Message = "Erreur lors de l'envoi du code." };
            }
        }

        public Task<SubmitOrganizationRequestResponse> VerifyCodeAsync(
            string email,
            string code,
            CancellationToken cancellationToken = default)
        {
            var cacheKey = $"OrgRequestCode_{email.ToLowerInvariant()}";
            if (!_cache.TryGetValue(cacheKey, out string? cachedCode) || cachedCode != code)
            {
                return Task.FromResult(new SubmitOrganizationRequestResponse
                {
                    Success = false,
                    Message = "Le code de validation est incorrect ou a expiré."
                });
            }

            // Marquer l'e-mail comme validé dans le cache pour 15 minutes
            var validatedKey = $"OrgRequestEmailValidated_{email.ToLowerInvariant()}";
            _cache.Set(validatedKey, true, TimeSpan.FromMinutes(15));
            
            // Supprimer le code de validation à usage unique
            _cache.Remove(cacheKey);

            return Task.FromResult(new SubmitOrganizationRequestResponse
            {
                Success = true,
                Message = "Votre adresse e-mail a été validée avec succès !"
            });
        }

        public async Task<SubmitOrganizationRequestResponse> SubmitOrganizationRequestAsync(
            SubmitOrganizationRequest request,
            CancellationToken cancellationToken = default)
        {
            var validatedKey = $"OrgRequestEmailValidated_{request.Email.ToLowerInvariant()}";
            if (!_cache.TryGetValue(validatedKey, out bool isValidated) || !isValidated)
            {
                return new SubmitOrganizationRequestResponse
                {
                    Success = false,
                    Message = "L'adresse e-mail n'a pas été validée. Veuillez d'abord valider votre adresse e-mail."
                };
            }

            // Supprimer l'état validé après utilisation réussie
            _cache.Remove(validatedKey);

            _logger.LogInformation("Traitement d'une nouvelle demande d'organisation: {OrgName} de {FullName}", request.OrganizationName, request.FullName);

            var users = await _userRepository.GetAllAsync();
            var adminsToNotify = users
                .Where(u => u.IsActive && (
                    string.Equals(u.Role, UserRoles.SUPER_ADMIN, StringComparison.OrdinalIgnoreCase) ||
                    (string.Equals(u.Role, UserRoles.ADMIN_ORG, StringComparison.OrdinalIgnoreCase) && u.OrganizationId == 1)
                ))
                .ToList();

            if (adminsToNotify.Count == 0)
            {
                _logger.LogWarning("Aucun administrateur actif (SUPER_ADMIN ou ADMIN_ORG de l'organisation 1) trouvé pour traiter la demande d'organisation.");
                return new SubmitOrganizationRequestResponse
                {
                    Success = false,
                    Message = "Désolé, nous ne pouvons pas traiter votre demande pour le moment. Veuillez réessayer plus tard."
                };
            }

            var title = "Nouvelle demande d'organisation";
            var message = $"Client: {request.FullName}\nPoste: {request.JobTitle}\nOrganisation: {request.OrganizationName}\nType: {request.OrganizationType}\nPays: {request.Country}\nEmail: {request.Email}\nTel: {request.Phone}";

            foreach (var admin in adminsToNotify)
            {
                // In-app notification
                var notification = new Notification
                {
                    OrganizationId = admin.OrganizationId,
                    UserId = admin.Id,
                    SenderId = null,
                    Type = NotificationConstants.TypeSystemAlert,
                    Category = NotificationConstants.CategoryInfo,
                    Title = title,
                    Message = message,
                    Priority = NotificationConstants.PriorityHigh,
                    IsRead = false,
                    IsPushSent = false,
                    Channel = "INAPP",
                    IsArchived = false,
                    ReferenceType = "ORGANIZATION_REQUEST",
                    ReferenceId = null,
                    ActionUrl = "/super-admin/dashboard",
                    CreatedAt = DateTime.UtcNow
                };

                await _notificationRepository.CreateAsync(notification);

                // Email notification to administrators
                try
                {
                    await _emailService.SendEmailAsync(
                        admin.Email,
                        $"[QualiFlow] Nouvelle demande d'organisation - {request.OrganizationName}",
                        BuildEmailBody(request));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Échec envoi email demande d'organisation vers l'administrateur {Email}", admin.Email);
                }
            }

            // Email de confirmation au demandeur (le futur administrateur)
            try
            {
                var clientEmailBody = $"<div style='font-family: Arial, sans-serif; color: #333; padding: 20px; line-height: 1.6;'>" +
                                      $"<h2 style='color: #064e3b;'>Merci pour votre intérêt pour QualiFlow !</h2>" +
                                      $"<p>Bonjour <strong>{System.Net.WebUtility.HtmlEncode(request.FullName)}</strong>,</p>" +
                                      $"<p>Nous avons bien reçu votre demande de création d'un espace organisation pour <strong>{System.Net.WebUtility.HtmlEncode(request.OrganizationName)}</strong>.</p>" +
                                      $"<p>Les administrateurs de notre plateforme étudient actuellement votre demande. Nous vous contacterons très prochainement pour finaliser la configuration et l'ouverture de votre espace dédié.</p>" +
                                      $"<div style='background: #f8fafc; padding: 15px; border-radius: 8px; border: 1px solid #e2e8f0; margin: 20px 0;'>" +
                                      $"<h3 style='margin-top: 0; color: #0f172a;'>Récapitulatif de votre demande :</h3>" +
                                      $"<ul style='list-style: none; padding-left: 0;'>" +
                                      $"<li><strong>Organisation :</strong> {System.Net.WebUtility.HtmlEncode(request.OrganizationName)}</li>" +
                                      $"<li><strong>Type d'organisation :</strong> {System.Net.WebUtility.HtmlEncode(request.OrganizationType)}</li>" +
                                      $"<li><strong>Fonction :</strong> {System.Net.WebUtility.HtmlEncode(request.JobTitle)}</li>" +
                                      $"<li><strong>Téléphone :</strong> {System.Net.WebUtility.HtmlEncode(request.Phone)}</li>" +
                                      $"<li><strong>Pays :</strong> {System.Net.WebUtility.HtmlEncode(request.Country)}</li>" +
                                      $"</ul>" +
                                      $"</div>" +
                                      $"<p>À très bientôt,<br><strong>L'équipe QualiFlow</strong></p>" +
                                      $"<hr style='border: none; border-top: 1px solid #e2e8f0; margin-top: 30px;'>" +
                                      $"<p style='font-size: 11px; color: #94a3b8;'>Cet email est une confirmation automatique. Merci de ne pas y répondre directement.</p>" +
                                      $"</div>";

                await _emailService.SendEmailAsync(
                    request.Email,
                    $"[QualiFlow] Confirmation de votre demande de création d'espace - {request.OrganizationName}",
                    clientEmailBody);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Échec envoi email de confirmation de demande d'organisation vers le client {Email}", request.Email);
            }

            return new SubmitOrganizationRequestResponse
            {
                Success = true,
                Message = "Votre demande a été envoyée avec succès. Notre équipe vous contactera prochainement."
            };
        }

        private static string BuildEmailBody(SubmitOrganizationRequest request)
        {
            var builder = new StringBuilder();
            builder.AppendLine("<div style='font-family: Arial, sans-serif; color: #333;'>");
            builder.AppendLine("<h2 style='color: #064e3b;'>Nouvelle demande d'organisation QualiFlow</h2>");
            builder.AppendLine("<p>Un prospect a formulé une demande pour une nouvelle instance d'organisation.</p>");
            builder.AppendLine("<div style='background: #f8fafc; padding: 20px; border-radius: 10px; border: 1px solid #e2e8f0;'>");
            builder.AppendLine("<ul style='list-style: none; padding: 0;'>");
            builder.AppendLine($"<li style='margin-bottom: 10px;'><strong>Nom complet :</strong> {System.Net.WebUtility.HtmlEncode(request.FullName)}</li>");
            builder.AppendLine($"<li style='margin-bottom: 10px;'><strong>Organisation :</strong> {System.Net.WebUtility.HtmlEncode(request.OrganizationName)}</li>");
            builder.AppendLine($"<li style='margin-bottom: 10px;'><strong>Type d'organisation :</strong> {System.Net.WebUtility.HtmlEncode(request.OrganizationType)}</li>");
            builder.AppendLine($"<li style='margin-bottom: 10px;'><strong>Poste :</strong> {System.Net.WebUtility.HtmlEncode(request.JobTitle)}</li>");
            builder.AppendLine($"<li style='margin-bottom: 10px;'><strong>Pays :</strong> {System.Net.WebUtility.HtmlEncode(request.Country)}</li>");
            builder.AppendLine($"<li style='margin-bottom: 10px;'><strong>Email :</strong> <a href='mailto:{request.Email}'>{System.Net.WebUtility.HtmlEncode(request.Email)}</a></li>");
            builder.AppendLine($"<li style='margin-bottom: 10px;'><strong>Téléphone :</strong> {System.Net.WebUtility.HtmlEncode(request.Phone)}</li>");
            builder.AppendLine("</ul>");
            builder.AppendLine("<hr style='border: none; border-top: 1px solid #cbd5e1; margin: 20px 0;'>");
            builder.AppendLine("<p><strong>Message :</strong></p>");
            builder.AppendLine($"<p style='white-space: pre-wrap;'>{System.Net.WebUtility.HtmlEncode(request.Message)}</p>");
            builder.AppendLine("</div>");
            builder.AppendLine("<p style='margin-top: 20px; font-size: 12px; color: #64748b;'>Cet email a été généré automatiquement par le système QualiFlow.</p>");
            builder.AppendLine("</div>");
            return builder.ToString();
        }
    }
}
