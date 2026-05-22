using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class ProcessActorRepository : IProcessActorRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ProcessActorRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<ProcessActorDetails>> GetActorsByProcessIdAsync(int processId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    pa.UserId,
                    u.FirstName,
                    u.LastName,
                    u.Email,
                    u.Function,
                    pa.ActorType,
                    pa.AssignedAt
                FROM ProcessActors pa
                INNER JOIN Users u ON u.Id = pa.UserId
                WHERE pa.ProcessId = @ProcessId
                ORDER BY pa.AssignedAt DESC";

            return await connection.QueryAsync<ProcessActorDetails>(sql, new { ProcessId = processId });
        }

        public async Task<bool> AddActorIfMissingAsync(int processId, int organizationId, int userId, string actorType)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO ProcessActors (OrganizationId, ProcessId, UserId, ActorType, AssignedAt)
                VALUES (@OrganizationId, @ProcessId, @UserId, @ActorType, NOW())
                ON CONFLICT (ProcessId, UserId) DO NOTHING;";

            var rows = await connection.ExecuteAsync(sql, new
            {
                OrganizationId = organizationId,
                ProcessId = processId,
                UserId = userId,
                ActorType = actorType
            });

            return rows > 0;
        }

        public async Task ReplaceActorsAsync(int processId, int organizationId, IEnumerable<ProcessActor> actors)
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            const string deleteSql = "DELETE FROM ProcessActors WHERE ProcessId = @ProcessId";
            await connection.ExecuteAsync(deleteSql, new { ProcessId = processId }, transaction);

            var actorList = actors.ToList();
            if (actorList.Count > 0)
            {
                const string insertSql = @"
                    INSERT INTO ProcessActors (OrganizationId, ProcessId, UserId, ActorType, AssignedAt)
                    VALUES (@OrganizationId, @ProcessId, @UserId, @ActorType, @AssignedAt);";

                await connection.ExecuteAsync(insertSql, actorList, transaction);
            }

            transaction.Commit();
        }

        public async Task<bool> RemoveActorAsync(int processId, int userId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM ProcessActors WHERE ProcessId = @ProcessId AND UserId = @UserId";
            var rows = await connection.ExecuteAsync(sql, new { ProcessId = processId, UserId = userId });
            return rows > 0;
        }

        public async Task<bool> HasActorAsync(int processId, int userId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT COUNT(1) FROM ProcessActors WHERE ProcessId = @ProcessId AND UserId = @UserId";
            var count = await connection.QuerySingleAsync<int>(sql, new { ProcessId = processId, UserId = userId });
            return count > 0;
        }
    }
}
