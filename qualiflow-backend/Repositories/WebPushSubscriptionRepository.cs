using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public sealed class WebPushSubscriptionRepository : IWebPushSubscriptionRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public WebPushSubscriptionRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<int> UpsertAsync(WebPushSubscription subscription)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO UserWebPushSubscriptions
                    (UserId, OrganizationId, Endpoint, P256dh, Auth, UserAgent, IsActive, CreatedAt, UpdatedAt, LastUsedAt)
                VALUES
                    (@UserId, @OrganizationId, @Endpoint, @P256dh, @Auth, @UserAgent, TRUE, NOW(), NOW(), NULL)
                ON CONFLICT (UserId, Endpoint)
                DO UPDATE SET
                    P256dh = EXCLUDED.P256dh,
                    Auth = EXCLUDED.Auth,
                    UserAgent = EXCLUDED.UserAgent,
                    IsActive = TRUE,
                    UpdatedAt = NOW()
                RETURNING Id;";

            return await connection.QuerySingleAsync<int>(sql, subscription);
        }

        public async Task<int> DeactivateAsync(int userId, string endpoint)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE UserWebPushSubscriptions
                SET IsActive = FALSE,
                    UpdatedAt = NOW()
                WHERE UserId = @UserId
                  AND Endpoint = @Endpoint
                  AND IsActive = TRUE;";

            return await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                Endpoint = endpoint
            });
        }

        public async Task<IReadOnlyList<WebPushSubscription>> GetActiveByUserAsync(int userId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT *
                FROM UserWebPushSubscriptions
                WHERE UserId = @UserId
                  AND IsActive = TRUE
                ORDER BY Id DESC;";

            var rows = await connection.QueryAsync<WebPushSubscription>(sql, new { UserId = userId });
            return rows.ToList();
        }
    }
}
