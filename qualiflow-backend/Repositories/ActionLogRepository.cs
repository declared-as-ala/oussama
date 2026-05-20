using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class ActionLogRepository : IActionLogRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ActionLogRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<int> CreateAsync(ActionLog log)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO ActionLogs (OrganizationId, Module, ActionType, Title, Description, PerformedByUserId, ActorName, CreatedAt)
                VALUES (@OrganizationId, @Module, @ActionType, @Title, @Description, @PerformedByUserId, @ActorName, NOW())
                RETURNING Id;";
            return await connection.QuerySingleAsync<int>(sql, log);
        }

        public async Task<List<ActionLog>> GetByOrganizationIdAsync(int organizationId, int limit = 50)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT * FROM ActionLogs
                WHERE OrganizationId = @OrganizationId
                ORDER BY CreatedAt DESC
                LIMIT @Limit;";
            return (await connection.QueryAsync<ActionLog>(sql, new { OrganizationId = organizationId, Limit = limit })).ToList();
        }
    }
}
