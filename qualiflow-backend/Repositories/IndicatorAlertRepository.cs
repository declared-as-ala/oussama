using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class IndicatorAlertRepository : IIndicatorAlertRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public IndicatorAlertRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<int> CreateAsync(IndicatorAlert alert)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO IndicatorAlerts
                    (OrganizationId, IndicatorId, IndicatorValueId, AlertType, Message, IsResolved, CreatedAt, ResolvedAt)
                VALUES
                    (@OrganizationId, @IndicatorId, @IndicatorValueId, @AlertType, @Message, @IsResolved, @CreatedAt, @ResolvedAt)
                RETURNING Id;";

            return await connection.QuerySingleAsync<int>(sql, alert);
        }

        public async Task<bool> ExistsOpenForValueAsync(int indicatorId, int indicatorValueId, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT COUNT(1)
                FROM IndicatorAlerts
                WHERE OrganizationId = @OrganizationId
                  AND IndicatorId = @IndicatorId
                  AND IndicatorValueId = @IndicatorValueId
                  AND IsResolved = FALSE";

            var count = await connection.QuerySingleAsync<int>(sql, new
            {
                OrganizationId = organizationId,
                IndicatorId = indicatorId,
                IndicatorValueId = indicatorValueId
            });

            return count > 0;
        }

        public async Task<int> ResolveOpenByIndicatorAsync(int indicatorId, int organizationId, DateTime resolvedAt)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                UPDATE IndicatorAlerts
                SET IsResolved = TRUE,
                    ResolvedAt = @ResolvedAt
                WHERE OrganizationId = @OrganizationId
                  AND IndicatorId = @IndicatorId
                  AND IsResolved = FALSE";

            return await connection.ExecuteAsync(sql, new
            {
                OrganizationId = organizationId,
                IndicatorId = indicatorId,
                ResolvedAt = resolvedAt
            });
        }

        public async Task<IEnumerable<IndicatorAlertData>> GetActiveAsync(int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    a.Id,
                    a.OrganizationId,
                    a.IndicatorId,
                    i.Code AS IndicatorCode,
                    i.Name AS IndicatorName,
                    a.IndicatorValueId,
                    a.AlertType,
                    a.Message,
                    a.IsResolved,
                    a.CreatedAt,
                    a.ResolvedAt,
                    iv.MeasuredValue,
                    iv.MeasuredAt,
                    i.TargetValue,
                    i.AlertThreshold
                FROM IndicatorAlerts a
                INNER JOIN Indicators i ON i.Id = a.IndicatorId
                INNER JOIN IndicatorValues iv ON iv.Id = a.IndicatorValueId
                WHERE a.OrganizationId = @OrganizationId
                  AND a.IsResolved = FALSE
                ORDER BY a.CreatedAt DESC, a.Id DESC";

            return await connection.QueryAsync<IndicatorAlertData>(sql, new
            {
                OrganizationId = organizationId
            });
        }

        public async Task<IEnumerable<IndicatorAlertData>> GetByIndicatorIdAsync(int indicatorId, int organizationId, bool? isResolved = null, int? limit = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            var limitClause = limit.HasValue ? "LIMIT @Limit" : string.Empty;
            var sql = $@"
                SELECT
                    a.Id,
                    a.OrganizationId,
                    a.IndicatorId,
                    i.Code AS IndicatorCode,
                    i.Name AS IndicatorName,
                    a.IndicatorValueId,
                    a.AlertType,
                    a.Message,
                    a.IsResolved,
                    a.CreatedAt,
                    a.ResolvedAt,
                    iv.MeasuredValue,
                    iv.MeasuredAt,
                    i.TargetValue,
                    i.AlertThreshold
                FROM IndicatorAlerts a
                INNER JOIN Indicators i ON i.Id = a.IndicatorId
                INNER JOIN IndicatorValues iv ON iv.Id = a.IndicatorValueId
                WHERE a.OrganizationId = @OrganizationId
                  AND a.IndicatorId = @IndicatorId
                  AND (@IsResolved IS NULL OR a.IsResolved = @IsResolved)
                ORDER BY a.CreatedAt DESC, a.Id DESC
                {limitClause}";

            return await connection.QueryAsync<IndicatorAlertData>(sql, new
            {
                OrganizationId = organizationId,
                IndicatorId = indicatorId,
                IsResolved = isResolved,
                Limit = limit
            });
        }
    }
}
