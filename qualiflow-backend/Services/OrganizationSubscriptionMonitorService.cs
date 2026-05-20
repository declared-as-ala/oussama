using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocApi.Services
{
    public sealed class OrganizationSubscriptionMonitorService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptionsMonitor<SubscriptionMonitorSettings> _settings;
        private readonly ILogger<OrganizationSubscriptionMonitorService> _logger;

        public OrganizationSubscriptionMonitorService(
            IServiceScopeFactory scopeFactory,
            IOptionsMonitor<SubscriptionMonitorSettings> settings,
            ILogger<OrganizationSubscriptionMonitorService> logger)
        {
            _scopeFactory = scopeFactory;
            _settings = settings;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OrganizationSubscriptionMonitorService démarre.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var currentSettings = _settings.CurrentValue;
                var intervalMinutes = Math.Max(currentSettings.PollingIntervalMinutes, 1);

                if (currentSettings.Enabled)
                {
                    try
                    {
                        await MonitorSubscriptionsAsync(stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erreur pendant le monitoring des abonnements d'organisations.");
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
        }

        private async Task MonitorSubscriptionsAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var organizationRepository = scope.ServiceProvider.GetRequiredService<IOrganizationRepository>();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var notificationPublisher = scope.ServiceProvider.GetRequiredService<INotificationEventPublisher>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var nowUtc = DateTime.UtcNow;
            await organizationRepository.DecrementSubscriptionDaysAsync(nowUtc);

            var expiredOrganizations = await organizationRepository.GetActiveExpiredSubscriptionsAsync();
            foreach (var organization in expiredOrganizations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var suspended = await organizationRepository.ToggleStatusAsync(organization.Id, "SUSPENDUE");
                if (!suspended)
                {
                    continue;
                }

                _logger.LogInformation(
                    "Organisation {OrganizationId} ({OrganizationName}) suspendue automatiquement: abonnement expiré.",
                    organization.Id,
                    organization.Name);

                if (!organization.SubscriptionExpiryAlertSent)
                {
                    await SendPlatformAlertAsync(notificationPublisher, organization.Id, organization.Name, cancellationToken);
                    await SendEmailAlertAsync(emailService, userRepository, organization.Id, organization.Name, cancellationToken);
                }

                await organizationRepository.MarkSubscriptionExpiryAlertSentAsync(organization.Id, true);
            }
        }

        private static Task SendPlatformAlertAsync(
            INotificationEventPublisher notificationPublisher,
            int organizationId,
            string organizationName,
            CancellationToken cancellationToken)
        {
            return notificationPublisher.PublishToRolesAsync(
                organizationId,
                new[] { UserRoles.ADMIN_ORG },
                NotificationConstants.TypeOrganizationSubscriptionExpired,
                NotificationConstants.CategoryError,
                "Abonnement expiré - Organisation suspendue",
                $"L'abonnement de l'organisation {organizationName} a atteint 0 jour. Le compte a été suspendu automatiquement.",
                NotificationConstants.PriorityCritical,
                "ORGANIZATION",
                organizationId.ToString(),
                "/profile",
                null,
                cancellationToken);
        }

        private async Task SendEmailAlertAsync(
            IEmailService emailService,
            IUserRepository userRepository,
            int organizationId,
            string organizationName,
            CancellationToken cancellationToken)
        {
            var admins = await userRepository.GetActiveByOrganizationAndRolesAsync(organizationId, new[] { UserRoles.ADMIN_ORG });
            var recipientEmails = admins
                .Select(user => user.Email?.Trim())
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (recipientEmails.Count == 0)
            {
                _logger.LogWarning(
                    "Aucun ADMIN_ORG trouvé pour envoyer l'alerte email d'abonnement expiré (OrganizationId={OrganizationId}).",
                    organizationId);
                return;
            }

            var subject = $"[QualiFlow] Abonnement expiré - {organizationName}";
            var body = $@"
                <p>Bonjour,</p>
                <p>L'abonnement de l'organisation <strong>{organizationName}</strong> est arrivé à <strong>0 jour</strong>.</p>
                <p>L'organisation a été suspendue automatiquement.</p>
                <p>Veuillez contacter le support pour réactiver l'abonnement.</p>
                <p>Cordialement,<br/>QualiFlow</p>";

            foreach (var email in recipientEmails)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await emailService.SendEmailAsync(email!, subject, body);
            }
        }
    }
}
