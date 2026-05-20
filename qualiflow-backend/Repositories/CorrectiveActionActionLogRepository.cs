using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class CorrectiveActionActionLogRepository : ICorrectiveActionActionLogRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public CorrectiveActionActionLogRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<int> CreateAsync(CorrectiveActionActionLog actionLog)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO CorrectiveActionActionLogs
                    (OrganizationId, CorrectiveActionId, ActionType, OldValue, NewValue, Comment, PerformedByUserId, PerformedAt)
                VALUES
                    (@OrganizationId, @CorrectiveActionId, @ActionType, @OldValue, @NewValue, @Comment, @PerformedByUserId, @PerformedAt)
                RETURNING Id;";

            return await connection.QuerySingleAsync<int>(sql, actionLog);
        }

        public async Task<IEnumerable<CorrectiveActionActionLogData>> GetByCorrectiveActionIdAsync(int correctiveActionId, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    l.Id,
                    l.OrganizationId,
                    l.CorrectiveActionId,
                    l.ActionType,
                    l.OldValue,
                    l.NewValue,
                    l.Comment,
                    l.PerformedByUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS PerformedByFullName,
                    l.PerformedAt
                FROM CorrectiveActionActionLogs l
                LEFT JOIN Users u ON u.Id = l.PerformedByUserId
                WHERE l.CorrectiveActionId = @CorrectiveActionId
                  AND l.OrganizationId = @OrganizationId
                ORDER BY l.PerformedAt DESC, l.Id DESC;";

            return await connection.QueryAsync<CorrectiveActionActionLogData>(sql, new
            {
                CorrectiveActionId = correctiveActionId,
                OrganizationId = organizationId
            });
        }

        public async Task<CorrectiveActionActionLog?> GetByIdAsync(int logId, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT * FROM CorrectiveActionActionLogs
                WHERE Id = @LogId AND OrganizationId = @OrganizationId;";

            return await connection.QueryFirstOrDefaultAsync<CorrectiveActionActionLog>(sql, new
            {
                LogId = logId,
                OrganizationId = organizationId
            });
        }

        public async Task<bool> DeleteAsync(int logId, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                DELETE FROM CorrectiveActionActionLogs
                WHERE Id = @LogId AND OrganizationId = @OrganizationId;";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                LogId = logId,
                OrganizationId = organizationId
            });

            return rowsAffected > 0;
        }
    }
}
