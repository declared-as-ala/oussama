using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace DocApi.Services
{
    public class NotificationGeneratorService : INotificationGeneratorService
    {
        private const string RuleDocumentExpired = "DOCUMENT_EXPIRED";
        private const string RuleDocumentApproval = "DOCUMENT_APPROVAL_REQUIRED";
        private const string RuleNonConformityCritical = "NONCONFORMITY_CRITICAL";
        private const string RuleCorrectiveActionDueSoon = "CORRECTIVE_ACTION_DUE_SOON";
        private const string RuleCorrectiveActionOverdue = "CORRECTIVE_ACTION_OVERDUE";
        private const string RuleProcessWithoutPilot = "PROCESS_WITHOUT_PILOT";
        private const string RuleProcedureWithoutResponsible = "PROCEDURE_WITHOUT_RESPONSIBLE";

        private readonly IDbConnectionFactory _connectionFactory;
        private readonly IUserRepository _userRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly IAlertRuleRepository _alertRuleRepository;
        private readonly IMemoryCache _cache;
        private readonly ILogger<NotificationGeneratorService> _logger;

        public NotificationGeneratorService(
            IDbConnectionFactory connectionFactory,
            IUserRepository userRepository,
            INotificationRepository notificationRepository,
            INotificationPublisher notificationPublisher,
            IAlertRuleRepository alertRuleRepository,
            IMemoryCache cache,
            ILogger<NotificationGeneratorService> logger)
        {
            _connectionFactory = connectionFactory;
            _userRepository = userRepository;
            _notificationRepository = notificationRepository;
            _notificationPublisher = notificationPublisher;
            _alertRuleRepository = alertRuleRepository;
            _cache = cache;
            _logger = logger;
        }

        public async Task GenerateAutomaticAlertsForUserAsync(UserContext userContext)
        {
            if (!userContext.OrganizationId.HasValue || userContext.IsSuperAdmin)
            {
                return;
            }

            var cacheKey = $"LastAutoAlertRun_User_{userContext.UserId}";
            if (_cache.TryGetValue(cacheKey, out _))
            {
                // Already ran recently, skip to prevent severe database load.
                return;
            }

            var organizationId = userContext.OrganizationId.Value;
            var now = DateTime.UtcNow;
            var dedupeWindowStart = now.AddHours(-12);

            var rules = await LoadRulesAsync(organizationId);
            var allActiveUsers = (await _userRepository.GetByOrganizationIdAsync(organizationId, 1, 5000))
                .Where(user => user.IsActive)
                .ToDictionary(user => user.Id, user => user);

            var qualityUsers = (await _userRepository.GetActiveByOrganizationAndRolesAsync(
                    organizationId,
                    new[] { UserRoles.ADMIN_ORG, UserRoles.RESPONSABLE_QUALITE }))
                .ToList();

            var qualityUserIds = qualityUsers.Select(user => user.Id).Distinct().ToList();
            if (qualityUserIds.Count == 0 && allActiveUsers.TryGetValue(userContext.UserId, out var currentUser))
            {
                qualityUserIds.Add(currentUser.Id);
            }

            var buffer = new List<Notification>();
            var runKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var connection = _connectionFactory.CreateConnection();

            if (IsRuleEnabled(rules, RuleNonConformityCritical))
            {
                var criticalNc = (await connection.QueryAsync<CriticalNonConformityData>(@"
                    SELECT Id, Code, Title
                    FROM NonConformities
                    WHERE OrganizationId = @OrganizationId
                      AND Severity = 'CRITIQUE'
                      AND Status IN ('OUVERTE', 'EN_COURS')
                    ORDER BY DetectedDate DESC, Id DESC",
                    new { OrganizationId = organizationId }))
                    .ToList();

                foreach (var nc in criticalNc)
                {
                    foreach (var userId in qualityUserIds)
                    {
                        await TryAddNotificationAsync(
                            buffer,
                            runKeys,
                            userId,
                            organizationId,
                            NotificationConstants.TypeNonConformityCritical,
                            NotificationConstants.CategoryError,
                            NotificationConstants.PriorityCritical,
                            $"Non-conformite critique {nc.Code}",
                            $"{nc.Title}. Traitement prioritaire requis.",
                            "NON_CONFORMITY",
                            nc.Id.ToString(),
                            $"/non-conformities/{nc.Id}",
                            dedupeWindowStart,
                            now);
                    }
                }
            }

            if (IsRuleEnabled(rules, RuleCorrectiveActionOverdue))
            {
                var overdueActions = (await connection.QueryAsync<CorrectiveActionAlertData>(@"
                    SELECT
                        ca.Id AS ActionId,
                        ca.NonConformityId,
                        ca.Title,
                        ca.ResponsibleUserId,
                        ca.DueDate,
                        nc.Code AS NonConformityCode
                    FROM CorrectiveActions ca
                    INNER JOIN NonConformities nc ON nc.Id = ca.NonConformityId
                    WHERE ca.OrganizationId = @OrganizationId
                      AND (
                          CASE
                              WHEN ca.Status = 'A_FAIRE' THEN 'PLANIFIEE'
                              WHEN ca.Status = 'TERMINEE' THEN 'REALISEE'
                              WHEN ca.Status = 'EN_RETARD' THEN 'EN_COURS'
                              ELSE ca.Status
                          END
                      ) NOT IN ('REALISEE', 'VERIFIEE')
                      AND ca.DueDate < NOW()
                    ORDER BY ca.DueDate ASC, ca.Id ASC",
                    new { OrganizationId = organizationId }))
                    .ToList();

                foreach (var action in overdueActions)
                {
                    if (!allActiveUsers.ContainsKey(action.ResponsibleUserId))
                    {
                        continue;
                    }

                    await TryAddNotificationAsync(
                        buffer,
                        runKeys,
                        action.ResponsibleUserId,
                        organizationId,
                        NotificationConstants.TypeCorrectiveActionOverdue,
                        NotificationConstants.CategoryWarning,
                        NotificationConstants.PriorityHigh,
                        $"Action corrective en retard ({action.NonConformityCode})",
                        $"{action.Title} depasse la date d'echeance du {action.DueDate:dd/MM/yyyy}.",
                        "CORRECTIVE_ACTION",
                        action.ActionId.ToString(),
                        $"/corrective-actions/{action.ActionId}",
                        dedupeWindowStart,
                        now);
                }
            }

            if (IsRuleEnabled(rules, RuleCorrectiveActionDueSoon))
            {
                var dueSoonActions = (await connection.QueryAsync<CorrectiveActionAlertData>(@"
                    SELECT
                        ca.Id AS ActionId,
                        ca.NonConformityId,
                        ca.Title,
                        ca.ResponsibleUserId,
                        ca.DueDate,
                        nc.Code AS NonConformityCode
                    FROM CorrectiveActions ca
                    INNER JOIN NonConformities nc ON nc.Id = ca.NonConformityId
                    WHERE ca.OrganizationId = @OrganizationId
                      AND (
                          CASE
                              WHEN ca.Status = 'A_FAIRE' THEN 'PLANIFIEE'
                              WHEN ca.Status = 'TERMINEE' THEN 'REALISEE'
                              WHEN ca.Status = 'EN_RETARD' THEN 'EN_COURS'
                              ELSE ca.Status
                          END
                      ) IN ('PLANIFIEE', 'EN_COURS')
                      AND ca.DueDate >= NOW()
                      AND ca.DueDate <= (NOW() + INTERVAL '2 days')
                    ORDER BY ca.DueDate ASC, ca.Id ASC",
                    new { OrganizationId = organizationId }))
                    .ToList();

                foreach (var action in dueSoonActions)
                {
                    if (!allActiveUsers.ContainsKey(action.ResponsibleUserId))
                    {
                        continue;
                    }

                    await TryAddNotificationAsync(
                        buffer,
                        runKeys,
                        action.ResponsibleUserId,
                        organizationId,
                        NotificationConstants.TypeCorrectiveActionDueSoon,
                        NotificationConstants.CategoryInfo,
                        NotificationConstants.PriorityMedium,
                        $"Echeance proche ({action.NonConformityCode})",
                        $"{action.Title} arrive a echeance le {action.DueDate:dd/MM/yyyy}.",
                        "CORRECTIVE_ACTION",
                        action.ActionId.ToString(),
                        $"/corrective-actions/{action.ActionId}",
                        dedupeWindowStart,
                        now);
                }
            }

            if (IsRuleEnabled(rules, RuleDocumentExpired))
            {
                var expiredDocuments = (await connection.QueryAsync<DocumentAlertData>(@"
                    SELECT
                        d.Id AS DocumentId,
                        d.Code,
                        d.Title,
                        d.OwnerUserId,
                        dv.VersionNumber,
                        dv.Status,
                        dv.ExpiryDate
                    FROM Documents d
                    INNER JOIN DocumentVersions dv ON dv.Id = d.CurrentVersionId
                    WHERE d.OrganizationId = @OrganizationId
                      AND d.IsActive = TRUE
                      AND (
                          dv.Status = 'PERIME'
                          OR (dv.ExpiryDate IS NOT NULL AND dv.ExpiryDate < NOW())
                      )
                    ORDER BY d.UpdatedAt DESC NULLS LAST, d.Id DESC",
                    new { OrganizationId = organizationId }))
                    .ToList();

                foreach (var document in expiredDocuments)
                {
                    var targets = ResolveDocumentTargets(document.OwnerUserId, qualityUserIds, allActiveUsers);
                    foreach (var targetUserId in targets)
                    {
                        await TryAddNotificationAsync(
                            buffer,
                            runKeys,
                            targetUserId,
                            organizationId,
                            NotificationConstants.TypeDocumentExpired,
                            NotificationConstants.CategoryWarning,
                            NotificationConstants.PriorityHigh,
                            $"Document perime {document.Code}",
                            $"{document.Title} ({document.VersionNumber}) est perime ou depasse sa validite.",
                            "DOCUMENT",
                            document.DocumentId.ToString(),
                            $"/documents/{document.DocumentId}",
                            dedupeWindowStart,
                            now);
                    }
                }
            }

            if (IsRuleEnabled(rules, RuleDocumentApproval))
            {
                var inReviewDocuments = (await connection.QueryAsync<DocumentAlertData>(@"
                    SELECT
                        d.Id AS DocumentId,
                        d.Code,
                        d.Title,
                        d.OwnerUserId,
                        dv.VersionNumber,
                        dv.Status,
                        dv.ExpiryDate
                    FROM Documents d
                    INNER JOIN DocumentVersions dv ON dv.Id = d.CurrentVersionId
                    WHERE d.OrganizationId = @OrganizationId
                      AND d.IsActive = TRUE
                      AND dv.Status = 'EN_REVISION'
                    ORDER BY d.UpdatedAt DESC NULLS LAST, d.Id DESC",
                    new { OrganizationId = organizationId }))
                    .ToList();

                foreach (var document in inReviewDocuments)
                {
                    var targets = ResolveDocumentTargets(document.OwnerUserId, qualityUserIds, allActiveUsers);
                    foreach (var targetUserId in targets)
                    {
                        await TryAddNotificationAsync(
                            buffer,
                            runKeys,
                            targetUserId,
                            organizationId,
                            NotificationConstants.TypeDocumentApprovalRequired,
                            NotificationConstants.CategoryInfo,
                            NotificationConstants.PriorityMedium,
                            $"Validation requise {document.Code}",
                            $"{document.Title} ({document.VersionNumber}) est en revision et attend une validation.",
                            "DOCUMENT",
                            document.DocumentId.ToString(),
                            $"/documents/{document.DocumentId}/versions",
                            dedupeWindowStart,
                            now);
                    }
                }
            }

            if (IsRuleEnabled(rules, RuleProcessWithoutPilot))
            {
                var processes = (await connection.QueryAsync<ProcessWithoutPilotData>(@"
                    SELECT Id, Code, Name
                    FROM Processes
                    WHERE OrganizationId = @OrganizationId
                      AND Status = 'ACTIF'
                      AND PilotUserId IS NULL
                    ORDER BY CreatedAt DESC, Id DESC",
                    new { OrganizationId = organizationId }))
                    .ToList();

                foreach (var process in processes)
                {
                    foreach (var userId in qualityUserIds)
                    {
                        await TryAddNotificationAsync(
                            buffer,
                            runKeys,
                            userId,
                            organizationId,
                            NotificationConstants.TypeProcessWithoutPilot,
                            NotificationConstants.CategoryWarning,
                            NotificationConstants.PriorityHigh,
                            $"Processus sans pilote {process.Code}",
                            $"{process.Name} doit etre assigne a un pilote actif.",
                            "PROCESS",
                            process.Id.ToString(),
                            $"/processes/{process.Id}",
                            dedupeWindowStart,
                            now);
                    }
                }
            }

            if (IsRuleEnabled(rules, RuleProcedureWithoutResponsible))
            {
                var procedures = (await connection.QueryAsync<ProcedureWithoutResponsibleData>(@"
                    SELECT Id, Code, Title
                    FROM Procedures
                    WHERE OrganizationId = @OrganizationId
                      AND Status = 'ACTIF'
                      AND ResponsibleUserId IS NULL
                    ORDER BY CreatedAt DESC, Id DESC",
                    new { OrganizationId = organizationId }))
                    .ToList();

                foreach (var procedure in procedures)
                {
                    foreach (var userId in qualityUserIds)
                    {
                        await TryAddNotificationAsync(
                            buffer,
                            runKeys,
                            userId,
                            organizationId,
                            NotificationConstants.TypeProcedureWithoutResponsible,
                            NotificationConstants.CategoryWarning,
                            NotificationConstants.PriorityHigh,
                            $"Procedure sans responsable {procedure.Code}",
                            $"{procedure.Title} n'a pas encore de responsable assigne.",
                            "PROCEDURE",
                            procedure.Id.ToString(),
                            $"/procedures/{procedure.Id}",
                            dedupeWindowStart,
                            now);
                    }
                }
            }

            if (buffer.Count > 0)
            {
                foreach (var notification in buffer)
                {
                    try
                    {
                        await _notificationPublisher.PublishAsync(MapToEventMessage(notification, userContext.UserId));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Publication RabbitMQ impossible pour notification auto type {Type}, user {UserId}.",
                            notification.Type,
                            notification.UserId);
                    }
                }
            }

            // Generation succeeded, set cache expiration to 5 minutes to throttle future runs
            _cache.Set(cacheKey, true, TimeSpan.FromMinutes(5));
        }

        private async Task<Dictionary<string, bool>> LoadRulesAsync(int organizationId)
        {
            var rules = await _alertRuleRepository.GetAllAsync(organizationId);
            return rules
                .GroupBy(rule => rule.Code.Trim().ToUpperInvariant())
                .ToDictionary(group => group.Key, group => group.OrderByDescending(rule => rule.CreatedAt).First().IsActive);
        }

        private static bool IsRuleEnabled(IReadOnlyDictionary<string, bool> rules, string code)
        {
            return !rules.TryGetValue(code.Trim().ToUpperInvariant(), out var isActive) || isActive;
        }

        private static IEnumerable<int> ResolveDocumentTargets(
            int? ownerUserId,
            IEnumerable<int> fallbackUserIds,
            IReadOnlyDictionary<int, User> allActiveUsers)
        {
            if (ownerUserId.HasValue && allActiveUsers.ContainsKey(ownerUserId.Value))
            {
                return new[] { ownerUserId.Value };
            }

            return fallbackUserIds.Distinct();
        }

        private async Task TryAddNotificationAsync(
            List<Notification> buffer,
            HashSet<string> runKeys,
            int userId,
            int organizationId,
            string type,
            string category,
            string priority,
            string title,
            string message,
            string? referenceType,
            string? referenceId,
            string? actionUrl,
            DateTime dedupeWindowStartUtc,
            DateTime createdAtUtc)
        {
            if (userId <= 0)
            {
                return;
            }

            var normalizedType = type.Trim().ToUpperInvariant();
            if (!NotificationConstants.AllowedTypes.Contains(normalizedType))
            {
                return;
            }

            var normalizedCategory = category.Trim().ToUpperInvariant();
            if (!NotificationConstants.AllowedCategories.Contains(normalizedCategory))
            {
                normalizedCategory = NotificationConstants.CategoryInfo;
            }

            var normalizedPriority = priority.Trim().ToUpperInvariant();
            if (!NotificationConstants.AllowedPriorities.Contains(normalizedPriority))
            {
                normalizedPriority = NotificationConstants.PriorityMedium;
            }

            var key = $"{userId}|{normalizedType}|{referenceType ?? string.Empty}|{referenceId ?? string.Empty}";
            if (runKeys.Contains(key))
            {
                return;
            }

            var alreadyExists = await _notificationRepository.ExistsSimilarInWindowAsync(
                userId,
                normalizedType,
                referenceType,
                referenceId,
                dedupeWindowStartUtc);

            if (alreadyExists)
            {
                return;
            }

            runKeys.Add(key);

            buffer.Add(new Notification
            {
                OrganizationId = organizationId,
                UserId = userId,
                Type = normalizedType,
                Category = normalizedCategory,
                Priority = normalizedPriority,
                Title = title,
                Message = message,
                IsRead = false,
                IsArchived = false,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                ActionUrl = actionUrl,
                CreatedAt = createdAtUtc
            });
        }

        private static NotificationEventMessage MapToEventMessage(Notification notification, int triggeredByUserId)
        {
            return new NotificationEventMessage
            {
                OrganizationId = notification.OrganizationId,
                UserId = notification.UserId,
                SenderId = triggeredByUserId,
                Type = notification.Type,
                Category = notification.Category,
                Title = notification.Title,
                Message = notification.Message,
                Priority = notification.Priority,
                EntityType = notification.EntityType ?? notification.ReferenceType,
                EntityId = notification.EntityId,
                RedirectUrl = notification.RedirectUrl ?? notification.ActionUrl,
                ExpiresAt = notification.ExpiresAt,
                ReferenceType = notification.ReferenceType,
                ReferenceId = notification.ReferenceId,
                ActionUrl = notification.ActionUrl,
                TriggeredByUserId = triggeredByUserId,
                TriggeredAt = notification.CreatedAt
            };
        }

        private sealed class CriticalNonConformityData
        {
            public int Id { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
        }

        private sealed class CorrectiveActionAlertData
        {
            public int ActionId { get; set; }
            public int NonConformityId { get; set; }
            public string Title { get; set; } = string.Empty;
            public int ResponsibleUserId { get; set; }
            public DateTime DueDate { get; set; }
            public string NonConformityCode { get; set; } = string.Empty;
        }

        private sealed class DocumentAlertData
        {
            public int DocumentId { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public int? OwnerUserId { get; set; }
            public string VersionNumber { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public DateTime? ExpiryDate { get; set; }
        }

        private sealed class ProcessWithoutPilotData
        {
            public int Id { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        private sealed class ProcedureWithoutResponsibleData
        {
            public int Id { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
        }
    }
}
