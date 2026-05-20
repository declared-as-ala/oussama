using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class ProcessActionLogRepository : IProcessActionLogRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ProcessActionLogRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<int> CreateAsync(ProcessActionLog actionLog)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO ProcessActionLogs
                    (OrganizationId, ProcessId, ActionType, OldValue, NewValue, Comment, PerformedByUserId, PerformedAt)
                VALUES
                    (@OrganizationId, @ProcessId, @ActionType, @OldValue, @NewValue, @Comment, @PerformedByUserId, @PerformedAt)
                RETURNING Id;";

            return await connection.QuerySingleAsync<int>(sql, actionLog);
        }

        public async Task<IEnumerable<ProcessActionLogData>> GetByProcessIdAsync(int processId, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    l.Id,
                    l.OrganizationId,
                    l.ProcessId,
                    l.ActionType,
                    l.OldValue,
                    l.NewValue,
                    l.Comment,
                    l.PerformedByUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS PerformedByFullName,
                    l.PerformedAt
                FROM ProcessActionLogs l
                LEFT JOIN Users u ON u.Id = l.PerformedByUserId
                WHERE l.ProcessId = @ProcessId
                  AND l.OrganizationId = @OrganizationId
                ORDER BY l.PerformedAt DESC, l.Id DESC;";

            return await connection.QueryAsync<ProcessActionLogData>(sql, new
            {
                ProcessId = processId,
                OrganizationId = organizationId
            });
        }

        public async Task<ProcessActionLog?> GetByIdAsync(int logId, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT * FROM ProcessActionLogs
                WHERE Id = @LogId AND OrganizationId = @OrganizationId;";

            return await connection.QueryFirstOrDefaultAsync<ProcessActionLog>(sql, new
            {
                LogId = logId,
                OrganizationId = organizationId
            });
        }

        public async Task<bool> DeleteAsync(int logId, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                DELETE FROM ProcessActionLogs
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
