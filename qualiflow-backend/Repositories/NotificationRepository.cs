using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;
using Npgsql;

namespace DocApi.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public NotificationRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<Notification>> SearchAsync(
            int pageNumber,
            int pageSize,
            int userId,
            int? organizationId,
            string? search,
            bool? isRead,
            string? category,
            string? priority,
            string? type,
            DateTime? fromDate,
            DateTime? toDate,
            bool includeArchived = false)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = new DynamicParameters();
            var whereClause = BuildWhereClause(
                parameters,
                userId,
                organizationId,
                search,
                isRead,
                category,
                priority,
                type,
                fromDate,
                toDate,
                includeArchived);

            parameters.Add("@PageSize", pageSize);
            parameters.Add("@Offset", (pageNumber - 1) * pageSize);

            var sql = $@"
                SELECT *
                FROM Notifications
                {whereClause}
                ORDER BY CreatedAt DESC, Id DESC
                LIMIT @PageSize OFFSET @Offset";

            return await connection.QueryAsync<Notification>(sql, parameters);
        }

        public async Task<int> CountSearchAsync(
            int userId,
            int? organizationId,
            string? search,
            bool? isRead,
            string? category,
            string? priority,
            string? type,
            DateTime? fromDate,
            DateTime? toDate,
            bool includeArchived = false)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = new DynamicParameters();
            var whereClause = BuildWhereClause(
                parameters,
                userId,
                organizationId,
                search,
                isRead,
                category,
                priority,
                type,
                fromDate,
                toDate,
                includeArchived);

            var sql = $@"
                SELECT COUNT(*)
                FROM Notifications
                {whereClause}";

            return await connection.QuerySingleAsync<int>(sql, parameters);
        }

        public async Task<IEnumerable<Notification>> GetForUserAsync(
            int userId,
            int? organizationId,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool includeArchived = true)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = new DynamicParameters();
            var whereClause = BuildWhereClause(
                parameters,
                userId,
                organizationId,
                null,
                null,
                null,
                null,
                null,
                fromDate,
                toDate,
                includeArchived);

            var sql = $@"
                SELECT *
                FROM Notifications n
                {whereClause}
                ORDER BY n.CreatedAt DESC, n.Id DESC";

            return await connection.QueryAsync<Notification>(sql, parameters);
        }

        public async Task<Notification?> GetByIdAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM Notifications WHERE Id = @Id";
            return await connection.QueryFirstOrDefaultAsync<Notification>(sql, new { Id = id });
        }

        public async Task<int> CreateAsync(Notification notification)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO Notifications
                    (OrganizationId, UserId, SenderId, Type, Category, Title, Message, Priority, IsRead, ReadAt, IsPushSent, Channel, ExternalProviderId, IsArchived, DocumentId, EntityType, EntityId, SourceModule, RedirectUrl, ReferenceType, ReferenceId, ActionUrl, ExpiresAt, TargetRole, EmailSent, EmailSentAt, EmailError, CreatedAt)
                VALUES
                    (@OrganizationId, @UserId, @SenderId, @Type, @Category, @Title, @Message, @Priority, @IsRead, @ReadAt, @IsPushSent, @Channel, @ExternalProviderId, @IsArchived, @DocumentId, @EntityType, @EntityId, @SourceModule, @RedirectUrl, @ReferenceType, @ReferenceId, @ActionUrl, @ExpiresAt, @TargetRole, @EmailSent, @EmailSentAt, @EmailError, @CreatedAt)
                RETURNING Id;";

            try
            {
                return await connection.QuerySingleAsync<int>(sql, notification);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return 0;
            }
        }

        public async Task<int> CreateBatchAsync(IEnumerable<Notification> notifications)
        {
            var list = notifications.ToList();
            if (list.Count == 0)
            {
                return 0;
            }

            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO Notifications
                    (OrganizationId, UserId, SenderId, Type, Category, Title, Message, Priority, IsRead, ReadAt, IsPushSent, Channel, ExternalProviderId, IsArchived, DocumentId, EntityType, EntityId, SourceModule, RedirectUrl, ReferenceType, ReferenceId, ActionUrl, ExpiresAt, TargetRole, EmailSent, EmailSentAt, EmailError, CreatedAt)
                VALUES
                    (@OrganizationId, @UserId, @SenderId, @Type, @Category, @Title, @Message, @Priority, @IsRead, @ReadAt, @IsPushSent, @Channel, @ExternalProviderId, @IsArchived, @DocumentId, @EntityType, @EntityId, @SourceModule, @RedirectUrl, @ReferenceType, @ReferenceId, @ActionUrl, @ExpiresAt, @TargetRole, @EmailSent, @EmailSentAt, @EmailError, @CreatedAt);";

            return await connection.ExecuteAsync(sql, list);
        }

        public async Task<bool> MarkAsReadAsync(int id, int userId, DateTime readAtUtc)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE Notifications
                SET IsRead = TRUE,
                    ReadAt = @ReadAt
                WHERE Id = @Id
                  AND UserId = @UserId";

            var rows = await connection.ExecuteAsync(sql, new
            {
                Id = id,
                UserId = userId,
                ReadAt = readAtUtc
            });

            return rows > 0;
        }

        public async Task<int> MarkAllAsReadAsync(int userId, int? organizationId, DateTime readAtUtc)
        {
            using var connection = _connectionFactory.CreateConnection();

            var sql = @"
                UPDATE Notifications
                SET IsRead = TRUE,
                    ReadAt = @ReadAt
                WHERE UserId = @UserId
                  AND IsRead = FALSE
                  AND IsArchived = FALSE";

            if (organizationId.HasValue)
            {
                sql += " AND (OrganizationId = @OrganizationId OR OrganizationId IS NULL)";
            }

            return await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                OrganizationId = organizationId,
                ReadAt = readAtUtc
            });
        }

        public async Task<bool> ArchiveAsync(int id, int userId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE Notifications
                SET IsArchived = TRUE,
                    IsRead = TRUE,
                    ReadAt = COALESCE(ReadAt, NOW())
                WHERE Id = @Id
                  AND UserId = @UserId";

            var rows = await connection.ExecuteAsync(sql, new { Id = id, UserId = userId });
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(int id, int userId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                DELETE FROM Notifications
                WHERE Id = @Id
                  AND UserId = @UserId";

            var rows = await connection.ExecuteAsync(sql, new { Id = id, UserId = userId });
            return rows > 0;
        }

        public async Task<int> GetUnreadCountAsync(int userId, int? organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            var sql = @"
                SELECT COUNT(1)
                FROM Notifications
                WHERE UserId = @UserId
                  AND IsArchived = FALSE
                  AND IsRead = FALSE";

            if (organizationId.HasValue)
            {
                sql += " AND (OrganizationId = @OrganizationId OR OrganizationId IS NULL)";
            }

            return await connection.QuerySingleAsync<int>(sql, new { UserId = userId, OrganizationId = organizationId });
        }

        public async Task<bool> MarkPushSentAsync(int id, string? externalProviderId, string channel)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE Notifications
                SET IsPushSent = TRUE,
                    ExternalProviderId = @ExternalProviderId,
                    Channel = @Channel
                WHERE Id = @Id;";

            var rows = await connection.ExecuteAsync(sql, new
            {
                Id = id,
                ExternalProviderId = externalProviderId,
                Channel = string.IsNullOrWhiteSpace(channel) ? "PUSH" : channel.Trim().ToUpperInvariant()
            });
            return rows > 0;
        }

        public async Task<bool> ExistsSimilarInWindowAsync(
            int userId,
            string type,
            string? referenceType,
            string? referenceId,
            DateTime fromUtc)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT COUNT(1)
                FROM Notifications
                WHERE UserId = @UserId
                  AND Type = @Type
                  AND COALESCE(ReferenceType, '') = COALESCE(@ReferenceType, '')
                  AND COALESCE(ReferenceId, '') = COALESCE(@ReferenceId, '')
                  AND CreatedAt >= @FromUtc
                  AND IsArchived = FALSE";

            var count = await connection.QuerySingleAsync<int>(sql, new
            {
                UserId = userId,
                Type = type,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                FromUtc = fromUtc
            });

            return count > 0;
        }

        private static string BuildWhereClause(
            DynamicParameters parameters,
            int userId,
            int? organizationId,
            string? search,
            bool? isRead,
            string? category,
            string? priority,
            string? type,
            DateTime? fromDate,
            DateTime? toDate,
            bool includeArchived)
        {
            var conditions = new List<string>
            {
                "UserId = @UserId"
            };

            parameters.Add("@UserId", userId);

            if (organizationId.HasValue)
            {
                conditions.Add("(OrganizationId = @OrganizationId OR OrganizationId IS NULL)");
                parameters.Add("@OrganizationId", organizationId.Value);
            }

            if (!includeArchived)
            {
                conditions.Add("IsArchived = FALSE");
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                conditions.Add(@"(
                    Title ILIKE @Search
                    OR Message ILIKE @Search
                    OR Type ILIKE @Search
                )");
                parameters.Add("@Search", $"%{search.Trim()}%");
            }

            if (isRead.HasValue)
            {
                conditions.Add("IsRead = @IsRead");
                parameters.Add("@IsRead", isRead.Value);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                conditions.Add("Category = @Category");
                parameters.Add("@Category", category.Trim());
            }

            if (!string.IsNullOrWhiteSpace(priority))
            {
                conditions.Add("Priority = @Priority");
                parameters.Add("@Priority", priority.Trim());
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                conditions.Add("Type = @Type");
                parameters.Add("@Type", type.Trim());
            }

            if (fromDate.HasValue)
            {
                conditions.Add("CreatedAt >= @FromDate");
                parameters.Add("@FromDate", fromDate.Value);
            }

            if (toDate.HasValue)
            {
                conditions.Add("CreatedAt <= @ToDate");
                parameters.Add("@ToDate", toDate.Value);
            }

            return $"WHERE {string.Join(" AND ", conditions)}";
        }
    }
}
