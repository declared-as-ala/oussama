using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class IndicatorActionLogRepository : IIndicatorActionLogRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public IndicatorActionLogRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<int> CreateAsync(IndicatorActionLog actionLog)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO IndicatorActionLogs
                    (OrganizationId, IndicatorId, ActionType, OldValue, NewValue, Comment, PerformedByUserId, PerformedAt)
                VALUES
                    (@OrganizationId, @IndicatorId, @ActionType, @OldValue, @NewValue, @Comment, @PerformedByUserId, @PerformedAt)
                RETURNING Id;";

            return await connection.QuerySingleAsync<int>(sql, actionLog);
        }

        public async Task<IEnumerable<IndicatorActionLogData>> GetByIndicatorIdAsync(int indicatorId, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    l.Id,
                    l.OrganizationId,
                    l.IndicatorId,
                    l.ActionType,
                    l.OldValue,
                    l.NewValue,
                    l.Comment,
                    l.PerformedByUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS PerformedByFullName,
                    l.PerformedAt
                FROM IndicatorActionLogs l
                LEFT JOIN Users u ON u.Id = l.PerformedByUserId
                WHERE l.IndicatorId = @IndicatorId
                  AND l.OrganizationId = @OrganizationId
                ORDER BY l.PerformedAt DESC, l.Id DESC;";

            return await connection.QueryAsync<IndicatorActionLogData>(sql, new
            {
                IndicatorId = indicatorId,
                OrganizationId = organizationId
            });
        }

        public async Task<IndicatorActionLog?> GetByIdAsync(int logId, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT * FROM IndicatorActionLogs
                WHERE Id = @LogId AND OrganizationId = @OrganizationId;";

            return await connection.QueryFirstOrDefaultAsync<IndicatorActionLog>(sql, new
            {
                LogId = logId,
                OrganizationId = organizationId
            });
        }

        public async Task<bool> DeleteAsync(int logId, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                DELETE FROM IndicatorActionLogs
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
