using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class ProcedureActionLogRepository : IProcedureActionLogRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ProcedureActionLogRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<int> CreateAsync(ProcedureActionLog actionLog)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO ProcedureActionLogs
                    (OrganizationId, ProcedureId, ActionType, OldValue, NewValue, Comment, PerformedByUserId, PerformedAt)
                VALUES
                    (@OrganizationId, @ProcedureId, @ActionType, @OldValue, @NewValue, @Comment, @PerformedByUserId, @PerformedAt)
                RETURNING Id;";

            return await connection.QuerySingleAsync<int>(sql, actionLog);
        }

        public async Task<IEnumerable<ProcedureActionLogData>> GetByProcedureIdAsync(int procedureId, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    l.Id,
                    l.OrganizationId,
                    l.ProcedureId,
                    l.ActionType,
                    l.OldValue,
                    l.NewValue,
                    l.Comment,
                    l.PerformedByUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS PerformedByFullName,
                    l.PerformedAt
                FROM ProcedureActionLogs l
                LEFT JOIN Users u ON u.Id = l.PerformedByUserId
                WHERE l.ProcedureId = @ProcedureId
                  AND l.OrganizationId = @OrganizationId
                ORDER BY l.PerformedAt DESC, l.Id DESC;";

            return await connection.QueryAsync<ProcedureActionLogData>(sql, new
            {
                ProcedureId = procedureId,
                OrganizationId = organizationId
            });
        }

        public async Task<ProcedureActionLog?> GetByIdAsync(int logId, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT * FROM ProcedureActionLogs
                WHERE Id = @LogId AND OrganizationId = @OrganizationId;";

            return await connection.QueryFirstOrDefaultAsync<ProcedureActionLog>(sql, new
            {
                LogId = logId,
                OrganizationId = organizationId
            });
        }

        public async Task<bool> DeleteAsync(int logId, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                DELETE FROM ProcedureActionLogs
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
