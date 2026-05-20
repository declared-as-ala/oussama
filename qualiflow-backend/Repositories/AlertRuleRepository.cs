using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class AlertRuleRepository : IAlertRuleRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public AlertRuleRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<AlertRule>> GetAllAsync(int? organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT *
                FROM AlertRules
                WHERE @OrganizationId IS NULL
                   OR OrganizationId = @OrganizationId
                   OR OrganizationId IS NULL
                ORDER BY IsActive DESC, CreatedAt DESC, Id DESC";

            return await connection.QueryAsync<AlertRule>(sql, new { OrganizationId = organizationId });
        }

        public async Task<AlertRule?> GetByIdAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM AlertRules WHERE Id = @Id";
            return await connection.QueryFirstOrDefaultAsync<AlertRule>(sql, new { Id = id });
        }

        public async Task<bool> ExistsCodeAsync(int organizationId, string code, int? excludeId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT COUNT(1)
                FROM AlertRules
                WHERE OrganizationId = @OrganizationId
                  AND LOWER(Code) = LOWER(@Code)
                  AND (@ExcludeId IS NULL OR Id <> @ExcludeId)";

            var count = await connection.QuerySingleAsync<int>(sql, new
            {
                OrganizationId = organizationId,
                Code = code,
                ExcludeId = excludeId
            });

            return count > 0;
        }

        public async Task<int> CreateAsync(AlertRule alertRule)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO AlertRules
                    (OrganizationId, Code, Name, Description, EntityType, TriggerType, IsActive, ThresholdValue, CreatedAt, UpdatedAt)
                VALUES
                    (@OrganizationId, @Code, @Name, @Description, @EntityType, @TriggerType, @IsActive, @ThresholdValue, @CreatedAt, @UpdatedAt)
                RETURNING Id;";

            return await connection.QuerySingleAsync<int>(sql, alertRule);
        }

        public async Task<bool> UpdateAsync(AlertRule alertRule)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                UPDATE AlertRules
                SET Code = @Code,
                    Name = @Name,
                    Description = @Description,
                    EntityType = @EntityType,
                    TriggerType = @TriggerType,
                    IsActive = @IsActive,
                    ThresholdValue = @ThresholdValue,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id";

            var rows = await connection.ExecuteAsync(sql, alertRule);
            return rows > 0;
        }

        public async Task<bool> ToggleStatusAsync(int id, bool isActive)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                UPDATE AlertRules
                SET IsActive = @IsActive,
                    UpdatedAt = NOW()
                WHERE Id = @Id";

            var rows = await connection.ExecuteAsync(sql, new { Id = id, IsActive = isActive });
            return rows > 0;
        }
    }
}
