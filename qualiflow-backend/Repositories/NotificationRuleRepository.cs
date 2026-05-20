using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class NotificationRuleRepository : INotificationRuleRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public NotificationRuleRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IReadOnlyList<NotificationRule>> GetByEventTypeAsync(int organizationId, string eventType)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT
                    id AS Id,
                    organization_id AS OrganizationId,
                    event_type AS EventType,
                    role_type AS RoleType,
                    email_enabled AS EmailEnabled,
                    in_app_enabled AS InAppEnabled,
                    is_active AS IsActive,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt
                FROM notification_rules
                WHERE organization_id = @OrganizationId
                  AND event_type = @EventType
                  AND is_active = TRUE
                ORDER BY id ASC;";

            var normalizedEventType = eventType.Trim();
            var items = await connection.QueryAsync<NotificationRule>(sql, new
            {
                OrganizationId = organizationId,
                EventType = normalizedEventType
            });

            return items.ToList();
        }
    }
}
