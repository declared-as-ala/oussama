using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class NotificationPreferenceRepository : INotificationPreferenceRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public NotificationPreferenceRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<NotificationPreference>> GetByUserIdAsync(int userId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT *
                FROM NotificationPreferences
                WHERE UserId = @UserId
                ORDER BY NotificationType";

            return await connection.QueryAsync<NotificationPreference>(sql, new { UserId = userId });
        }

        public async Task<NotificationPreference?> GetByUserAndTypeAsync(int userId, string notificationType)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT *
                FROM NotificationPreferences
                WHERE UserId = @UserId
                  AND NotificationType = @NotificationType";

            return await connection.QueryFirstOrDefaultAsync<NotificationPreference>(sql, new
            {
                UserId = userId,
                NotificationType = notificationType
            });
        }

        public async Task<int> UpsertAsync(NotificationPreference preference)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO NotificationPreferences
                    (UserId, NotificationType, InAppEnabled, EmailEnabled, CreatedAt, UpdatedAt)
                VALUES
                    (@UserId, @NotificationType, @InAppEnabled, @EmailEnabled, @CreatedAt, @UpdatedAt)
                ON CONFLICT (UserId, NotificationType)
                DO UPDATE SET
                    InAppEnabled = EXCLUDED.InAppEnabled,
                    EmailEnabled = EXCLUDED.EmailEnabled,
                    UpdatedAt = NOW()
                RETURNING Id;";

            var now = DateTime.UtcNow;

            return await connection.QuerySingleAsync<int>(sql, new
            {
                preference.UserId,
                preference.NotificationType,
                preference.InAppEnabled,
                preference.EmailEnabled,
                CreatedAt = preference.CreatedAt == default ? now : preference.CreatedAt,
                UpdatedAt = preference.UpdatedAt ?? now
            });
        }
    }
}
