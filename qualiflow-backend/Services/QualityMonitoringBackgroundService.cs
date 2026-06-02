using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocApi.Services
{
    public sealed class QualityMonitoringBackgroundService : BackgroundService
    {
        private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(5);
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<QualityMonitoringBackgroundService> _logger;

        public QualityMonitoringBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<QualityMonitoringBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("QualityMonitoringBackgroundService démarré. Intervalle: {IntervalMinutes}m", PollingInterval.TotalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunChecksAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur pendant l'exécution des vérifications automatiques de qualité.");
                }

                await Task.Delay(PollingInterval, stoppingToken);
            }
        }

        private async Task RunChecksAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            var nonConformityRepository = scope.ServiceProvider.GetRequiredService<INonConformityRepository>();
            var notificationPublisher = scope.ServiceProvider.GetRequiredService<INotificationEventPublisher>();
            var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
            var actionLogger = scope.ServiceProvider.GetRequiredService<IActionLogger>();

            await MonitorExpiredDocumentsAsync(connectionFactory, nonConformityRepository, notificationPublisher, actionLogger, cancellationToken);
            await MonitorOverdueActionsAsync(connectionFactory, notificationPublisher, cache, cancellationToken);
        }

        private async Task MonitorExpiredDocumentsAsync(
            IDbConnectionFactory connectionFactory,
            INonConformityRepository nonConformityRepository,
            INotificationEventPublisher notificationPublisher,
            IActionLogger actionLogger,
            CancellationToken cancellationToken)
        {
            using var connection = connectionFactory.CreateConnection();
            
            // Find all active documents that are expired (either Status is PERIME or ExpiryDate < NOW)
            var expiredDocuments = (await connection.QueryAsync<ExpiredDocumentDto>(@"
                SELECT
                    d.Id AS DocumentId,
                    d.OrganizationId,
                    d.Code,
                    d.Title,
                    d.OwnerUserId,
                    d.ProcessId,
                    d.ProcedureId,
                    dv.VersionNumber,
                    dv.Status,
                    dv.ExpiryDate
                FROM Documents d
                INNER JOIN DocumentVersions dv ON dv.Id = d.CurrentVersionId
                WHERE d.IsActive = TRUE
                  AND (
                      dv.Status = 'PERIME'
                      OR (dv.ExpiryDate IS NOT NULL AND dv.ExpiryDate < NOW())
                  )
                ORDER BY d.Id DESC")).ToList();

            foreach (var doc in expiredDocuments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var ncTitle = $"[Système] Expiration du document {doc.Code}".Trim();
                
                // Check if a non-conformity already exists for this document expiration
                var exists = await connection.ExecuteScalarAsync<int>(@"
                    SELECT COUNT(1)
                    FROM NonConformities
                    WHERE OrganizationId = @OrgId
                      AND Title = @Title", new { OrgId = doc.OrganizationId, Title = ncTitle }) > 0;

                if (exists)
                {
                    continue;
                }

                // Generate NC code
                string ncCode = await nonConformityRepository.GenerateNextCodeAsync(doc.OrganizationId);

                // Define actor user ID for Action Log.
                // Fallback to first active user in the organization if no owner exists.
                int actorUserId = doc.OwnerUserId ?? await connection.QueryFirstOrDefaultAsync<int>(@"
                    SELECT Id FROM Users WHERE OrganizationId = @OrgId AND IsActive = TRUE LIMIT 1",
                    new { OrgId = doc.OrganizationId });

                if (actorUserId <= 0)
                {
                    _logger.LogWarning("Impossible de créer la non-conformité automatique pour le document {DocCode} : aucun utilisateur actif trouvé dans l'organisation.", doc.Code);
                    continue;
                }

                // Create the non-conformity
                var nc = new NonConformity
                {
                    OrganizationId = doc.OrganizationId,
                    Code = ncCode,
                    Title = ncTitle,
                    Description = $"Alerte automatique : Le document '{doc.Title}' (Code: {doc.Code}, Version: {doc.VersionNumber}) a expiré le {(doc.ExpiryDate.HasValue ? doc.ExpiryDate.Value.ToString("dd/MM/yyyy") : "inconnue")}.",
                    Type = "INTERNE",
                    Severity = "MAJEURE",
                    ProcessId = doc.ProcessId,
                    ProcedureId = doc.ProcedureId,
                    DetectedDate = DateTime.UtcNow,
                    ResponsibleUserId = doc.OwnerUserId,
                    Status = "OUVERTE",
                    CreatedAt = DateTime.UtcNow
                };

                var ncId = await nonConformityRepository.CreateAsync(nc);

                _logger.LogInformation("Non-conformité {NcCode} créée automatiquement pour le document expiré {DocCode}.", ncCode, doc.Code);

                await actionLogger.LogActionAsync(
                    doc.OrganizationId,
                    actorUserId,
                    "Système",
                    "NON_CONFORMITY",
                    "CREATE",
                    $"Création auto NC {ncCode} (Document expiré)",
                    $"La non-conformité '{ncTitle}' a été générée automatiquement suite à l'expiration du document.");

                // Trigger alerts
                // 1. Alert owner
                if (doc.OwnerUserId.HasValue)
                {
                    await notificationPublisher.PublishToUserAsync(
                        doc.OrganizationId,
                        doc.OwnerUserId.Value,
                        NotificationConstants.TypeDocumentExpired,
                        NotificationConstants.CategoryWarning,
                        "Votre document a expiré",
                        $"Le document {doc.Code} a expiré. Une non-conformité ({ncCode}) a été ouverte automatiquement.",
                        NotificationConstants.PriorityHigh,
                        "NON_CONFORMITY",
                        ncId.ToString(),
                        $"/non-conformities/{ncId}",
                        null,
                        cancellationToken);
                }

                // 2. Alert Admin & Quality Manager
                await notificationPublisher.PublishToRolesAsync(
                    doc.OrganizationId,
                    new[] { UserRoles.ADMIN_ORG, UserRoles.RESPONSABLE_QUALITE },
                    NotificationConstants.TypeNonConformityCreated,
                    NotificationConstants.CategoryError,
                    "Document expiré - NC ouverte",
                    $"Le document {doc.Code} a expiré. La non-conformité {ncCode} a été ouverte automatiquement.",
                    NotificationConstants.PriorityHigh,
                    "NON_CONFORMITY",
                    ncId.ToString(),
                    $"/non-conformities/{ncId}",
                    null,
                    cancellationToken);
            }
        }

        private async Task MonitorOverdueActionsAsync(
            IDbConnectionFactory connectionFactory,
            INotificationEventPublisher notificationPublisher,
            IMemoryCache cache,
            CancellationToken cancellationToken)
        {
            using var connection = connectionFactory.CreateConnection();

            // Find all overdue corrective actions (DueDate < NOW and Status NOT IN REALISEE, VERIFIEE)
            var overdueActions = (await connection.QueryAsync<OverdueActionDto>(@"
                SELECT
                    ca.Id AS ActionId,
                    ca.OrganizationId,
                    ca.Title,
                    ca.ResponsibleUserId,
                    ca.DueDate,
                    nc.Code AS NonConformityCode
                FROM CorrectiveActions ca
                INNER JOIN NonConformities nc ON nc.Id = ca.NonConformityId
                WHERE (
                    CASE
                        WHEN ca.Status = 'A_FAIRE' THEN 'PLANIFIEE'
                        WHEN ca.Status = 'TERMINEE' THEN 'REALISEE'
                        WHEN ca.Status = 'EN_RETARD' THEN 'EN_COURS'
                        ELSE ca.Status
                    END
                ) NOT IN ('REALISEE', 'VERIFIEE')
                AND ca.DueDate < NOW()
                ORDER BY ca.DueDate ASC")).ToList();

            foreach (var action in overdueActions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Prevent daily flooding for the same overdue corrective action using memory cache
                string cacheKey = $"OverdueActionAlertSent_{action.ActionId}_{DateTime.UtcNow:yyyyMMdd}";
                if (cache.TryGetValue(cacheKey, out _))
                {
                    continue;
                }

                // Notify Admins & Quality Managers
                await notificationPublisher.PublishToRolesAsync(
                    action.OrganizationId,
                    new[] { UserRoles.ADMIN_ORG, UserRoles.RESPONSABLE_QUALITE },
                    NotificationConstants.TypeCorrectiveActionOverdue,
                    NotificationConstants.CategoryError,
                    "Action corrective en retard",
                    $"L'action '{action.Title}' associée à la non-conformité {action.NonConformityCode} est en retard (échéance : {action.DueDate:dd/MM/yyyy}).",
                    NotificationConstants.PriorityHigh,
                    "CORRECTIVE_ACTION",
                    action.ActionId.ToString(),
                    $"/corrective-actions/{action.ActionId}",
                    null,
                    cancellationToken);

                // Cache it so we only alert once per day for this action
                cache.Set(cacheKey, true, TimeSpan.FromDays(1));
            }
        }

        private class ExpiredDocumentDto
        {
            public int DocumentId { get; set; }
            public int OrganizationId { get; set; }
            public string? Code { get; set; }
            public string? Title { get; set; }
            public int? OwnerUserId { get; set; }
            public int? ProcessId { get; set; }
            public int? ProcedureId { get; set; }
            public string? VersionNumber { get; set; }
            public string? Status { get; set; }
            public DateTime? ExpiryDate { get; set; }
        }

        private class OverdueActionDto
        {
            public int ActionId { get; set; }
            public int OrganizationId { get; set; }
            public string? Title { get; set; }
            public int ResponsibleUserId { get; set; }
            public DateTime DueDate { get; set; }
            public string? NonConformityCode { get; set; }
        }
    }
}
