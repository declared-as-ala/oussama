using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DocApi.Services
{
    public sealed class NotificationDispatcher : INotificationDispatcher
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly IEmailService _emailService;
        private readonly ILogger<NotificationDispatcher> _logger;

        public NotificationDispatcher(
            IDbConnectionFactory connectionFactory,
            IEmailService emailService,
            ILogger<NotificationDispatcher> logger)
        {
            _connectionFactory = connectionFactory;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<int> DispatchPendingNotificationsAsync(CancellationToken cancellationToken = default)
        {
            var dispatchedCount = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                using var connection = _connectionFactory.CreateConnection();
                connection.Open();
                using var transaction = connection.BeginTransaction();

                var notification = await connection.QueryFirstOrDefaultAsync<Notification>(
                    @"
                    SELECT Id,
                           OrganizationId,
                          UserId,
                           Title,
                           Message,
                           Type,
                           TargetRole,
                           EmailSent,
                           EmailAttemptCount,
                           EmailNextAttemptAt,
                           CreatedAt
                    FROM Notifications
                    WHERE EmailSent = FALSE
                      AND (EmailNextAttemptAt IS NULL OR EmailNextAttemptAt <= NOW())
                      AND (
                          (TargetRole IS NOT NULL AND TRIM(TargetRole) <> '')
                          OR UserId > 0
                      )
                    ORDER BY CreatedAt ASC, Id ASC
                    FOR UPDATE SKIP LOCKED
                    LIMIT 1;",
                    transaction: transaction);

                if (notification is null)
                {
                    transaction.Commit();
                    break;
                }

                try
                {
                    var recipients = !string.IsNullOrWhiteSpace(notification.TargetRole)
                        ? await GetRecipientsByRoleAsync(connection, transaction, notification.OrganizationId, notification.TargetRole!)
                        : await GetRecipientByUserAsync(connection, transaction, notification.OrganizationId, notification.UserId);

                    if (!recipients.Any())
                    {
                        await connection.ExecuteAsync(
                            @"
                            UPDATE Notifications
                            SET EmailSent = TRUE,
                                EmailSentAt = NOW(),
                                EmailError = @EmailError
                            WHERE Id = @Id
                              AND EmailSent = FALSE;",
                            new
                            {
                                notification.Id,
                                EmailError = "Aucun destinataire actif trouvé pour cette notification (utilisateur/role/organisation)."
                            },
                            transaction);

                        transaction.Commit();
                        dispatchedCount++;
                        continue;
                    }

                    foreach (var recipient in recipients)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (IsNonDeliverableDemoAddress(recipient.Email))
                        {
                            _logger.LogInformation(
                                "Email notification {NotificationId} skipped for non-deliverable demo recipient {Recipient}.",
                                notification.Id,
                                recipient.Email);
                            continue;
                        }

                        var subject = notification.Title;
                        var body = BuildHtmlEmailBody(notification, recipient);
                        await _emailService.SendEmailAsync(recipient.Email, subject, body);
                    }

                    await connection.ExecuteAsync(
                        @"
                        UPDATE Notifications
                        SET EmailSent = TRUE,
                            EmailSentAt = NOW(),
                            EmailError = NULL
                        WHERE Id = @Id
                          AND EmailSent = FALSE;",
                        new { notification.Id },
                        transaction);

                    transaction.Commit();
                    dispatchedCount++;
                }
                catch (OperationCanceledException)
                {
                    transaction.Rollback();
                    throw;
                }
                catch (Exception ex)
                {
                    var emailError = TruncateError(ex.Message);
                    var emailAttemptCount = notification.EmailAttemptCount + 1;
                    var emailNextAttemptAt = GetNextEmailAttemptAt(ex, emailAttemptCount);
                    var isProviderCooldown = IsProviderCooldown(ex);

                    _logger.LogError(
                        ex,
                        "Erreur d'envoi email pour notification {NotificationId} (Org={OrganizationId}, Role={Role}). Prochaine tentative: {NextAttemptAt}.",
                        notification.Id,
                        notification.OrganizationId,
                        notification.TargetRole,
                        emailNextAttemptAt);

                    await connection.ExecuteAsync(
                        @"
                        UPDATE Notifications
                        SET EmailError = @EmailError,
                            EmailAttemptCount = @EmailAttemptCount,
                            EmailNextAttemptAt = @EmailNextAttemptAt,
                            EmailSent = FALSE
                        WHERE Id = @Id;",
                        new
                        {
                            notification.Id,
                            EmailError = emailError,
                            EmailAttemptCount = emailAttemptCount,
                            EmailNextAttemptAt = emailNextAttemptAt
                        },
                        transaction);

                    if (isProviderCooldown)
                    {
                        var postponedCount = await connection.ExecuteAsync(
                            @"
                            UPDATE Notifications
                            SET EmailNextAttemptAt = @EmailNextAttemptAt,
                                EmailError = @EmailError
                            WHERE EmailSent = FALSE
                              AND Id <> @Id
                              AND (EmailNextAttemptAt IS NULL OR EmailNextAttemptAt <= NOW());",
                            new
                            {
                                notification.Id,
                                EmailNextAttemptAt = emailNextAttemptAt,
                                EmailError = "SMTP provider temporarily rejected authentication. Retry postponed."
                            },
                            transaction);

                        _logger.LogWarning(
                            "SMTP provider cooldown detected. {PostponedCount} pending email notifications postponed until {NextAttemptAt}.",
                            postponedCount,
                            emailNextAttemptAt);
                    }

                    transaction.Commit();
                    break;
                }
            }

            if (dispatchedCount > 0)
            {
                _logger.LogInformation("Traitement notifications email terminé. Notifications traitées: {Count}", dispatchedCount);
            }

            return dispatchedCount;
        }

        private static string BuildHtmlEmailBody(Notification notification, NotificationRecipient recipient)
        {
            return EmailTemplateHelper.GetNotificationEmail(
                recipient.FullName,
                notification.Title,
                notification.Message,
                notification.Type,
                notification.CreatedAt);
        }

        private static string TruncateError(string? error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return "Erreur SMTP inconnue.";
            }

            const int maxLength = 2000;
            return error.Length <= maxLength ? error : error[..maxLength];
        }

        private static DateTime GetNextEmailAttemptAt(Exception ex, int attemptCount)
        {
            if (IsProviderCooldown(ex))
            {
                return DateTime.UtcNow.AddHours(1);
            }

            var backoffMinutes = Math.Min(60, Math.Pow(2, Math.Min(attemptCount, 6)));
            return DateTime.UtcNow.AddMinutes(backoffMinutes);
        }

        private static bool IsProviderCooldown(Exception ex)
        {
            var message = ex.ToString();

            return message.Contains("Too many login attempts", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("4.7.0", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("AuthenticateAsync", StringComparison.OrdinalIgnoreCase) &&
                   (message.Contains("SocketException (10053)", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("SocketException (10054)", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("connection was closed", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("connexion", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsNonDeliverableDemoAddress(string? email)
        {
            return !string.IsNullOrWhiteSpace(email) &&
                   email.EndsWith("@demo.local", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<List<NotificationRecipient>> GetRecipientsByRoleAsync(
            System.Data.IDbConnection connection,
            System.Data.IDbTransaction transaction,
            int? organizationId,
            string targetRole)
        {
            var normalizedRole = targetRole.Trim().ToUpperInvariant();

            try
            {
                var recipients = await connection.QueryAsync<NotificationRecipient>(
                    @"
                    SELECT DISTINCT
                           u.Id,
                           CONCAT(COALESCE(u.FirstName, ''), ' ', COALESCE(u.LastName, '')) AS FullName,
                           u.Email,
                           u.OrganizationId,
                           COALESCE(NULLIF(TRIM(r.Name), ''), u.Role) AS Role
                    FROM Users u
                    LEFT JOIN UserRoles ur ON ur.UserId = u.Id
                    LEFT JOIN Roles r ON r.Id = ur.RoleId
                    WHERE u.IsActive = TRUE
                      AND (@OrganizationId IS NULL OR u.OrganizationId = @OrganizationId)
                      AND (
                            UPPER(COALESCE(NULLIF(TRIM(r.Name), ''), '')) = @TargetRole
                            OR UPPER(COALESCE(NULLIF(TRIM(u.Role), ''), '')) = @TargetRole
                      )
                      AND u.Email IS NOT NULL
                      AND TRIM(u.Email) <> ''
                    ORDER BY u.Id;",
                    new
                    {
                        OrganizationId = organizationId,
                        TargetRole = normalizedRole
                    },
                    transaction);

                return recipients.ToList();
            }
            catch
            {
                var fallbackRecipients = await connection.QueryAsync<NotificationRecipient>(
                    @"
                    SELECT DISTINCT
                           u.Id,
                           CONCAT(COALESCE(u.FirstName, ''), ' ', COALESCE(u.LastName, '')) AS FullName,
                           u.Email,
                           u.OrganizationId,
                           u.Role
                    FROM Users u
                    WHERE u.IsActive = TRUE
                      AND (@OrganizationId IS NULL OR u.OrganizationId = @OrganizationId)
                      AND UPPER(COALESCE(NULLIF(TRIM(u.Role), ''), '')) = @TargetRole
                      AND u.Email IS NOT NULL
                      AND TRIM(u.Email) <> ''
                    ORDER BY u.Id;",
                    new
                    {
                        OrganizationId = organizationId,
                        TargetRole = normalizedRole
                    },
                    transaction);

                return fallbackRecipients.ToList();
            }
        }

        private static async Task<List<NotificationRecipient>> GetRecipientByUserAsync(
            System.Data.IDbConnection connection,
            System.Data.IDbTransaction transaction,
            int? organizationId,
            int userId)
        {
            if (userId <= 0)
            {
                return new List<NotificationRecipient>();
            }

            var recipient = await connection.QueryFirstOrDefaultAsync<NotificationRecipient>(
                @"
                SELECT
                       u.Id,
                       CONCAT(COALESCE(u.FirstName, ''), ' ', COALESCE(u.LastName, '')) AS FullName,
                       u.Email,
                       u.OrganizationId,
                       u.Role
                FROM Users u
                WHERE u.Id = @UserId
                  AND (@OrganizationId IS NULL OR u.OrganizationId = @OrganizationId)
                  AND u.IsActive = TRUE
                  AND u.Email IS NOT NULL
                  AND TRIM(u.Email) <> ''
                LIMIT 1;",
                new
                {
                    UserId = userId,
                    OrganizationId = organizationId
                },
                transaction);

            if (recipient == null)
            {
                return new List<NotificationRecipient>();
            }

            return new List<NotificationRecipient> { recipient };
        }
    }
}
