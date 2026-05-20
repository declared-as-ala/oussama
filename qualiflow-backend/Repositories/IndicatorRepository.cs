using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class IndicatorRepository : IIndicatorRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public IndicatorRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<IndicatorListItemData>> SearchAsync(
            int pageNumber,
            int pageSize,
            string? search,
            string? status,
            int? processId,
            string? measurementFrequency,
            int? responsibleUserId,
            bool? isInAlert,
            int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = new DynamicParameters();
            var whereClause = BuildWhereClause(
                parameters,
                search,
                status,
                processId,
                measurementFrequency,
                responsibleUserId,
                isInAlert,
                organizationId);

            parameters.Add("@PageSize", pageSize);
            parameters.Add("@Offset", (pageNumber - 1) * pageSize);

            var sql = $@"
                SELECT
                    i.Id,
                    i.OrganizationId,
                    i.ProcessId,
                    p.Code AS ProcessCode,
                    p.Name AS ProcessName,
                    i.Code,
                    i.Name,
                    i.Description,
                    i.CalculationMethod,
                    i.Unit,
                    i.TargetValue,
                    i.AlertThreshold,
                    i.MeasurementFrequency,
                    i.ResponsibleUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS ResponsibleFullName,
                    i.Status,
                    lv.MeasuredValue AS LatestValue,
                    lv.MeasuredAt AS LatestMeasuredAt,
                    CASE
                        WHEN lv.Id IS NOT NULL
                             AND (lv.MeasuredValue < i.AlertThreshold OR lv.MeasuredValue < i.TargetValue)
                        THEN TRUE
                        ELSE FALSE
                    END AS IsInAlert,
                    i.CreatedAt,
                    i.UpdatedAt
                FROM Indicators i
                INNER JOIN Processes p ON p.Id = i.ProcessId
                LEFT JOIN Users u ON u.Id = i.ResponsibleUserId
                LEFT JOIN LATERAL (
                    SELECT
                        iv.Id,
                        iv.MeasuredValue,
                        iv.MeasuredAt,
                        iv.CreatedAt
                    FROM IndicatorValues iv
                    WHERE iv.IndicatorId = i.Id
                      AND iv.OrganizationId = i.OrganizationId
                    ORDER BY iv.MeasuredAt DESC, iv.CreatedAt DESC, iv.Id DESC
                    LIMIT 1
                ) lv ON TRUE
                {whereClause}
                ORDER BY i.CreatedAt DESC, i.Id DESC
                LIMIT @PageSize OFFSET @Offset";

            return await connection.QueryAsync<IndicatorListItemData>(sql, parameters);
        }

        public async Task<int> CountSearchAsync(
            string? search,
            string? status,
            int? processId,
            string? measurementFrequency,
            int? responsibleUserId,
            bool? isInAlert,
            int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = new DynamicParameters();
            var whereClause = BuildWhereClause(
                parameters,
                search,
                status,
                processId,
                measurementFrequency,
                responsibleUserId,
                isInAlert,
                organizationId);

            var sql = $@"
                SELECT COUNT(1)
                FROM Indicators i
                INNER JOIN Processes p ON p.Id = i.ProcessId
                LEFT JOIN Users u ON u.Id = i.ResponsibleUserId
                LEFT JOIN LATERAL (
                    SELECT
                        iv.Id,
                        iv.MeasuredValue,
                        iv.MeasuredAt,
                        iv.CreatedAt
                    FROM IndicatorValues iv
                    WHERE iv.IndicatorId = i.Id
                      AND iv.OrganizationId = i.OrganizationId
                    ORDER BY iv.MeasuredAt DESC, iv.CreatedAt DESC, iv.Id DESC
                    LIMIT 1
                ) lv ON TRUE
                {whereClause}";

            return await connection.QuerySingleAsync<int>(sql, parameters);
        }

        public async Task<Indicator?> GetByIdAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM Indicators WHERE Id = @Id";
            return await connection.QueryFirstOrDefaultAsync<Indicator>(sql, new { Id = id });
        }

        public async Task<IndicatorDetailsData?> GetDetailsByIdAsync(int id, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    i.Id,
                    i.OrganizationId,
                    i.ProcessId,
                    p.Code AS ProcessCode,
                    p.Name AS ProcessName,
                    i.Code,
                    i.Name,
                    i.Description,
                    i.CalculationMethod,
                    i.Unit,
                    i.TargetValue,
                    i.AlertThreshold,
                    i.MeasurementFrequency,
                    i.ResponsibleUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS ResponsibleFullName,
                    u.Email AS ResponsibleEmail,
                    i.Status,
                    lv.MeasuredValue AS LatestValue,
                    lv.MeasuredAt AS LatestMeasuredAt,
                    CASE
                        WHEN lv.Id IS NOT NULL
                             AND (lv.MeasuredValue < i.AlertThreshold OR lv.MeasuredValue < i.TargetValue)
                        THEN TRUE
                        ELSE FALSE
                    END AS IsInAlert,
                    i.CreatedAt,
                    i.UpdatedAt
                FROM Indicators i
                INNER JOIN Processes p ON p.Id = i.ProcessId
                LEFT JOIN Users u ON u.Id = i.ResponsibleUserId
                LEFT JOIN LATERAL (
                    SELECT
                        iv.Id,
                        iv.MeasuredValue,
                        iv.MeasuredAt,
                        iv.CreatedAt
                    FROM IndicatorValues iv
                    WHERE iv.IndicatorId = i.Id
                      AND iv.OrganizationId = i.OrganizationId
                    ORDER BY iv.MeasuredAt DESC, iv.CreatedAt DESC, iv.Id DESC
                    LIMIT 1
                ) lv ON TRUE
                WHERE i.Id = @Id
                  AND i.OrganizationId = @OrganizationId";

            return await connection.QueryFirstOrDefaultAsync<IndicatorDetailsData>(sql, new
            {
                Id = id,
                OrganizationId = organizationId
            });
        }

        public async Task<IEnumerable<IndicatorListItemData>> GetByProcessAsync(int processId, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    i.Id,
                    i.OrganizationId,
                    i.ProcessId,
                    p.Code AS ProcessCode,
                    p.Name AS ProcessName,
                    i.Code,
                    i.Name,
                    i.Description,
                    i.CalculationMethod,
                    i.Unit,
                    i.TargetValue,
                    i.AlertThreshold,
                    i.MeasurementFrequency,
                    i.ResponsibleUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS ResponsibleFullName,
                    i.Status,
                    lv.MeasuredValue AS LatestValue,
                    lv.MeasuredAt AS LatestMeasuredAt,
                    CASE
                        WHEN lv.Id IS NOT NULL
                             AND (lv.MeasuredValue < i.AlertThreshold OR lv.MeasuredValue < i.TargetValue)
                        THEN TRUE
                        ELSE FALSE
                    END AS IsInAlert,
                    i.CreatedAt,
                    i.UpdatedAt
                FROM Indicators i
                INNER JOIN Processes p ON p.Id = i.ProcessId
                LEFT JOIN Users u ON u.Id = i.ResponsibleUserId
                LEFT JOIN LATERAL (
                    SELECT
                        iv.Id,
                        iv.MeasuredValue,
                        iv.MeasuredAt,
                        iv.CreatedAt
                    FROM IndicatorValues iv
                    WHERE iv.IndicatorId = i.Id
                      AND iv.OrganizationId = i.OrganizationId
                    ORDER BY iv.MeasuredAt DESC, iv.CreatedAt DESC, iv.Id DESC
                    LIMIT 1
                ) lv ON TRUE
                WHERE i.OrganizationId = @OrganizationId
                  AND i.ProcessId = @ProcessId
                ORDER BY i.CreatedAt DESC, i.Id DESC";

            return await connection.QueryAsync<IndicatorListItemData>(sql, new
            {
                OrganizationId = organizationId,
                ProcessId = processId
            });
        }

        public async Task<IEnumerable<IndicatorStatisticsData>> GetForStatisticsAsync(int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    i.ProcessId,
                    p.Name AS ProcessName,
                    i.Status,
                    i.MeasurementFrequency,
                    CASE
                        WHEN lv.Id IS NOT NULL
                             AND (lv.MeasuredValue < i.AlertThreshold OR lv.MeasuredValue < i.TargetValue)
                        THEN TRUE
                        ELSE FALSE
                    END AS IsInAlert
                FROM Indicators i
                INNER JOIN Processes p ON p.Id = i.ProcessId
                LEFT JOIN LATERAL (
                    SELECT
                        iv.Id,
                        iv.MeasuredValue,
                        iv.MeasuredAt,
                        iv.CreatedAt
                    FROM IndicatorValues iv
                    WHERE iv.IndicatorId = i.Id
                      AND iv.OrganizationId = i.OrganizationId
                    ORDER BY iv.MeasuredAt DESC, iv.CreatedAt DESC, iv.Id DESC
                    LIMIT 1
                ) lv ON TRUE
                WHERE i.OrganizationId = @OrganizationId";

            return await connection.QueryAsync<IndicatorStatisticsData>(sql, new { OrganizationId = organizationId });
        }

        public async Task<bool> ExistsCodeAsync(int organizationId, string code, int? excludeId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT COUNT(1)
                FROM Indicators
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

        public async Task<int> CreateAsync(Indicator indicator)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO Indicators
                    (OrganizationId, ProcessId, Code, Name, Description, CalculationMethod, Unit, TargetValue, AlertThreshold, MeasurementFrequency, ResponsibleUserId, Status, CreatedAt, UpdatedAt)
                VALUES
                    (@OrganizationId, @ProcessId, @Code, @Name, @Description, @CalculationMethod, @Unit, @TargetValue, @AlertThreshold, @MeasurementFrequency, @ResponsibleUserId, @Status, @CreatedAt, @UpdatedAt)
                RETURNING Id;";

            return await connection.QuerySingleAsync<int>(sql, indicator);
        }

        public async Task<bool> UpdateAsync(Indicator indicator)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                UPDATE Indicators
                SET ProcessId = @ProcessId,
                    Code = @Code,
                    Name = @Name,
                    Description = @Description,
                    CalculationMethod = @CalculationMethod,
                    Unit = @Unit,
                    TargetValue = @TargetValue,
                    AlertThreshold = @AlertThreshold,
                    MeasurementFrequency = @MeasurementFrequency,
                    ResponsibleUserId = @ResponsibleUserId,
                    Status = @Status,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id
                  AND OrganizationId = @OrganizationId";

            var rows = await connection.ExecuteAsync(sql, indicator);
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(int id, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                DELETE FROM Indicators
                WHERE Id = @Id
                  AND OrganizationId = @OrganizationId";

            var rows = await connection.ExecuteAsync(sql, new
            {
                Id = id,
                OrganizationId = organizationId
            });

            return rows > 0;
        }

        public async Task<bool> ToggleStatusAsync(int id, int organizationId, string status, DateTime updatedAt)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                UPDATE Indicators
                SET Status = @Status,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id
                  AND OrganizationId = @OrganizationId";

            var rows = await connection.ExecuteAsync(sql, new
            {
                Id = id,
                OrganizationId = organizationId,
                Status = status,
                UpdatedAt = updatedAt
            });

            return rows > 0;
        }

        private static string BuildWhereClause(
            DynamicParameters parameters,
            string? search,
            string? status,
            int? processId,
            string? measurementFrequency,
            int? responsibleUserId,
            bool? isInAlert,
            int organizationId)
        {
            var conditions = new List<string>
            {
                "i.OrganizationId = @OrganizationId"
            };

            parameters.Add("@OrganizationId", organizationId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                conditions.Add(@"(
                    i.Code ILIKE @Search
                    OR i.Name ILIKE @Search
                    OR COALESCE(i.Description, '') ILIKE @Search
                    OR COALESCE(u.FirstName, '') ILIKE @Search
                    OR COALESCE(u.LastName, '') ILIKE @Search
                )");
                parameters.Add("@Search", $"%{search.Trim()}%");
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                conditions.Add("i.Status = @Status");
                parameters.Add("@Status", status.Trim());
            }

            if (processId.HasValue)
            {
                conditions.Add("i.ProcessId = @ProcessId");
                parameters.Add("@ProcessId", processId.Value);
            }

            if (!string.IsNullOrWhiteSpace(measurementFrequency))
            {
                conditions.Add("i.MeasurementFrequency = @MeasurementFrequency");
                parameters.Add("@MeasurementFrequency", measurementFrequency.Trim());
            }

            if (responsibleUserId.HasValue)
            {
                conditions.Add("i.ResponsibleUserId = @ResponsibleUserId");
                parameters.Add("@ResponsibleUserId", responsibleUserId.Value);
            }

            if (isInAlert.HasValue)
            {
                if (isInAlert.Value)
                {
                    conditions.Add("(lv.Id IS NOT NULL AND (lv.MeasuredValue < i.AlertThreshold OR lv.MeasuredValue < i.TargetValue))");
                }
                else
                {
                    conditions.Add("(lv.Id IS NULL OR (lv.MeasuredValue >= i.AlertThreshold AND lv.MeasuredValue >= i.TargetValue))");
                }
            }

            return $"WHERE {string.Join(" AND ", conditions.Where(c => !string.IsNullOrWhiteSpace(c)))}";
        }
    }
}
