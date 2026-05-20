using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class IndicatorValueRepository : IIndicatorValueRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public IndicatorValueRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<IndicatorValueData>> GetByIndicatorIdAsync(int indicatorId, int organizationId, int? take = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            var limitClause = take.HasValue ? "LIMIT @Take" : string.Empty;
            var sql = $@"
                SELECT
                    iv.Id,
                    iv.OrganizationId,
                    iv.IndicatorId,
                    iv.PeriodLabel,
                    iv.MeasuredValue,
                    iv.Comment,
                    iv.MeasuredAt,
                    iv.EnteredByUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS EnteredByFullName,
                    iv.CreatedAt
                FROM IndicatorValues iv
                LEFT JOIN Users u ON u.Id = iv.EnteredByUserId
                WHERE iv.OrganizationId = @OrganizationId
                  AND iv.IndicatorId = @IndicatorId
                ORDER BY iv.MeasuredAt DESC, iv.CreatedAt DESC, iv.Id DESC
                {limitClause}";

            return await connection.QueryAsync<IndicatorValueData>(sql, new
            {
                OrganizationId = organizationId,
                IndicatorId = indicatorId,
                Take = take
            });
        }

        public async Task<IndicatorValueData?> GetByIdAsync(int valueId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    iv.Id,
                    iv.OrganizationId,
                    iv.IndicatorId,
                    iv.PeriodLabel,
                    iv.MeasuredValue,
                    iv.Comment,
                    iv.MeasuredAt,
                    iv.EnteredByUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS EnteredByFullName,
                    iv.CreatedAt
                FROM IndicatorValues iv
                LEFT JOIN Users u ON u.Id = iv.EnteredByUserId
                WHERE iv.Id = @ValueId";

            return await connection.QueryFirstOrDefaultAsync<IndicatorValueData>(sql, new { ValueId = valueId });
        }

        public async Task<IndicatorValueData?> GetLatestByIndicatorIdAsync(int indicatorId, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    iv.Id,
                    iv.OrganizationId,
                    iv.IndicatorId,
                    iv.PeriodLabel,
                    iv.MeasuredValue,
                    iv.Comment,
                    iv.MeasuredAt,
                    iv.EnteredByUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS EnteredByFullName,
                    iv.CreatedAt
                FROM IndicatorValues iv
                LEFT JOIN Users u ON u.Id = iv.EnteredByUserId
                WHERE iv.OrganizationId = @OrganizationId
                  AND iv.IndicatorId = @IndicatorId
                ORDER BY iv.MeasuredAt DESC, iv.CreatedAt DESC, iv.Id DESC
                LIMIT 1";

            return await connection.QueryFirstOrDefaultAsync<IndicatorValueData>(sql, new
            {
                OrganizationId = organizationId,
                IndicatorId = indicatorId
            });
        }

        public async Task<bool> ExistsPeriodAsync(int indicatorId, int organizationId, string periodLabel, int? excludeValueId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT COUNT(1)
                FROM IndicatorValues
                WHERE OrganizationId = @OrganizationId
                  AND IndicatorId = @IndicatorId
                  AND LOWER(TRIM(PeriodLabel)) = LOWER(TRIM(@PeriodLabel))
                  AND (@ExcludeValueId IS NULL OR Id <> @ExcludeValueId)";

            var count = await connection.QuerySingleAsync<int>(sql, new
            {
                OrganizationId = organizationId,
                IndicatorId = indicatorId,
                PeriodLabel = periodLabel,
                ExcludeValueId = excludeValueId
            });

            return count > 0;
        }

        public async Task<int> CreateAsync(IndicatorValue value)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO IndicatorValues
                    (OrganizationId, IndicatorId, PeriodLabel, MeasuredValue, Comment, MeasuredAt, EnteredByUserId, CreatedAt)
                VALUES
                    (@OrganizationId, @IndicatorId, @PeriodLabel, @MeasuredValue, @Comment, @MeasuredAt, @EnteredByUserId, @CreatedAt)
                RETURNING Id;";

            return await connection.QuerySingleAsync<int>(sql, value);
        }

        public async Task<bool> UpdateAsync(IndicatorValue value)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                UPDATE IndicatorValues
                SET PeriodLabel = @PeriodLabel,
                    MeasuredValue = @MeasuredValue,
                    Comment = @Comment,
                    MeasuredAt = @MeasuredAt
                WHERE Id = @Id
                  AND IndicatorId = @IndicatorId
                  AND OrganizationId = @OrganizationId";

            var rows = await connection.ExecuteAsync(sql, value);
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(int valueId, int indicatorId, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                DELETE FROM IndicatorValues
                WHERE Id = @ValueId
                  AND IndicatorId = @IndicatorId
                  AND OrganizationId = @OrganizationId";

            var rows = await connection.ExecuteAsync(sql, new
            {
                ValueId = valueId,
                IndicatorId = indicatorId,
                OrganizationId = organizationId
            });

            return rows > 0;
        }
    }
}
