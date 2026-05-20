using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class InstructionRepository : IInstructionRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public InstructionRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<Instruction>> GetByProcedureIdAsync(int procedureId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT *
                FROM Instructions
                WHERE ProcedureId = @ProcedureId
                ORDER BY OrderIndex ASC, Id ASC";

            return await connection.QueryAsync<Instruction>(sql, new { ProcedureId = procedureId });
        }

        public async Task<Instruction?> GetByIdAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM Instructions WHERE Id = @Id";
            return await connection.QueryFirstOrDefaultAsync<Instruction>(sql, new { Id = id });
        }

        public async Task<bool> ExistsCodeAsync(int procedureId, string code, int? excludeId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT COUNT(1)
                FROM Instructions
                WHERE ProcedureId = @ProcedureId
                  AND LOWER(Code) = LOWER(@Code)
                  AND (@ExcludeId IS NULL OR Id <> @ExcludeId)";

            var count = await connection.QuerySingleAsync<int>(sql, new
            {
                ProcedureId = procedureId,
                Code = code,
                ExcludeId = excludeId
            });

            return count > 0;
        }

        public async Task<int> GetNextOrderIndexAsync(int procedureId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT COALESCE(MAX(OrderIndex), 0) + 1
                FROM Instructions
                WHERE ProcedureId = @ProcedureId";

            return await connection.QuerySingleAsync<int>(sql, new { ProcedureId = procedureId });
        }

        public async Task<int> CreateAsync(Instruction instruction)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO Instructions
                    (OrganizationId, ProcedureId, Code, Title, Description, Status, OrderIndex, CreatedAt, UpdatedAt)
                VALUES
                    (@OrganizationId, @ProcedureId, @Code, @Title, @Description, @Status, @OrderIndex, @CreatedAt, @UpdatedAt)
                RETURNING Id;";

            return await connection.QuerySingleAsync<int>(sql, instruction);
        }

        public async Task<bool> UpdateAsync(Instruction instruction)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                UPDATE Instructions
                SET Code = @Code,
                    Title = @Title,
                    Description = @Description,
                    Status = @Status,
                    OrderIndex = @OrderIndex,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id";

            var rows = await connection.ExecuteAsync(sql, instruction);
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM Instructions WHERE Id = @Id";
            var rows = await connection.ExecuteAsync(sql, new { Id = id });
            return rows > 0;
        }
    }
}
