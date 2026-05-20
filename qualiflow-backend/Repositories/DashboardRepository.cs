using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DocApi.DTOs.Dashboard;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public DashboardRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<DashboardKpiResponse> GetKpisAsync(DashboardQueryParameters query)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = BuildFilters(query);
            var organizationWhere = BuildOrganizationWhereClause("o", includeOrganizationId: true);
            var userWhere = BuildUserWhereClause();
            var processWhere = BuildProcessWhereClause();

            var documentsTableExists = await TableExistsAsync("documents");

            var sql = $@"
                SELECT
                    (SELECT COUNT(1) FROM Organizations o {organizationWhere}) AS TotalOrganizations,
                    (SELECT COUNT(1) FROM Organizations o {organizationWhere} {(organizationWhere.Length > 0 ? "AND" : "WHERE")} o.Status = 'ACTIF') AS ActiveOrganizations,
                    (SELECT COUNT(1) FROM Organizations o {organizationWhere} {(organizationWhere.Length > 0 ? "AND" : "WHERE")} o.Status = 'SUSPENDUE') AS SuspendedOrganizations,
                    (SELECT COUNT(1)
                     FROM Users u
                     LEFT JOIN Organizations o ON o.Id = u.OrganizationId
                     {userWhere}) AS TotalUsers,
                    (SELECT COUNT(1)
                     FROM Users u
                     LEFT JOIN Organizations o ON o.Id = u.OrganizationId
                     {userWhere} {(userWhere.Length > 0 ? "AND" : "WHERE")} u.Role = 'ADMIN_ORG') AS TotalOrganizationAdmins,
                    (SELECT COUNT(1)
                     FROM Processes p
                     LEFT JOIN Organizations o ON o.Id = p.OrganizationId
                     {processWhere}) AS TotalProcesses;";

            var kpis = await connection.QuerySingleAsync<DashboardKpiResponse>(sql, parameters);

            if (documentsTableExists)
            {
                const string documentSql = @"
                    SELECT COUNT(1)
                    FROM Documents d
                    LEFT JOIN Organizations o ON o.Id = d.OrganizationId
                    WHERE (@OrganizationId IS NULL OR d.OrganizationId = @OrganizationId)
                      AND (@Status IS NULL OR o.Status = @Status)
                      AND (@Type IS NULL OR o.Type = @Type)";

                kpis.TotalDocuments = await connection.QuerySingleAsync<int>(documentSql, parameters);
            }

            // Cross-module tables are optional in this repository version.
            kpis.OpenNonConformities = 0;
            kpis.OverdueCorrectiveActions = 0;
            kpis.AlertIndicators = 0;

            return kpis;
        }

        public async Task<DashboardChartResponse> GetChartsAsync(DashboardQueryParameters query)
        {
            using var connection = _connectionFactory.CreateConnection();
            var response = new DashboardChartResponse();
            var parameters = BuildFilters(query);
            var organizationWhere = BuildOrganizationWhereClause("o", includeOrganizationId: true);
            var userWhere = BuildUserWhereClause();
            var months = ParsePeriodInMonths(query.Period);

            var organizationsByStatusSql = $@"
                SELECT o.Status AS Label, COUNT(1) AS Value
                FROM Organizations o
                {organizationWhere}
                GROUP BY o.Status
                ORDER BY o.Status";

            response.OrganizationsByStatus = (await connection.QueryAsync<DashboardChartDataPointResponse>(organizationsByStatusSql, parameters)).ToList();

            var organizationsByTypeSql = $@"
                SELECT COALESCE(o.Type, 'NON_DEFINI') AS Label, COUNT(1) AS Value
                FROM Organizations o
                {organizationWhere}
                GROUP BY COALESCE(o.Type, 'NON_DEFINI')
                ORDER BY Label";

            response.OrganizationsByType = (await connection.QueryAsync<DashboardChartDataPointResponse>(organizationsByTypeSql, parameters)).ToList();

            var usersByRoleSql = $@"
                SELECT u.Role AS Label, COUNT(1) AS Value
                FROM Users u
                LEFT JOIN Organizations o ON o.Id = u.OrganizationId
                {userWhere}
                GROUP BY u.Role
                ORDER BY u.Role";

            response.UsersByRole = (await connection.QueryAsync<DashboardChartDataPointResponse>(usersByRoleSql, parameters)).ToList();

            response.TopOrganizationsByUsers = await GetTopOrganizationsAsync(query, 5);
            response.TopOrganizationsByDocuments = response.TopOrganizationsByUsers.OrderByDescending(x => x.DocumentsCount).Take(5).ToList();
            response.TopOrganizationsByNonConformities = response.TopOrganizationsByUsers.OrderByDescending(x => x.NonConformitiesCount).Take(5).ToList();

            var monthlyOrganizationsSql = @"
                WITH months AS (
                    SELECT date_trunc('month', NOW()) - (INTERVAL '1 month' * generate_series(0, @Months - 1)) AS month_start
                )
                SELECT
                    to_char(m.month_start, 'YYYY-MM') AS Period,
                    COALESCE(COUNT(o.Id), 0)::int AS Value
                FROM months m
                LEFT JOIN Organizations o
                    ON date_trunc('month', o.CreatedAt) = m.month_start
                   AND (@OrganizationId IS NULL OR o.Id = @OrganizationId)
                   AND (@Status IS NULL OR o.Status = @Status)
                   AND (@Type IS NULL OR o.Type = @Type)
                GROUP BY m.month_start
                ORDER BY m.month_start";

            response.MonthlyOrganizationsCreated = (await connection.QueryAsync<DashboardMonthlyTrendPointResponse>(monthlyOrganizationsSql, new
            {
                parameters.OrganizationId,
                parameters.Status,
                parameters.Type,
                Months = months
            })).ToList();

            var monthlyUsersSql = @"
                WITH months AS (
                    SELECT date_trunc('month', NOW()) - (INTERVAL '1 month' * generate_series(0, @Months - 1)) AS month_start
                )
                SELECT
                    to_char(m.month_start, 'YYYY-MM') AS Period,
                    COALESCE(COUNT(u.Id), 0)::int AS Value
                FROM months m
                LEFT JOIN Users u
                    ON date_trunc('month', u.CreatedAt) = m.month_start
                LEFT JOIN Organizations o ON o.Id = u.OrganizationId
                WHERE (@OrganizationId IS NULL OR u.OrganizationId = @OrganizationId)
                  AND (@Status IS NULL OR o.Status = @Status)
                  AND (@Type IS NULL OR o.Type = @Type)
                GROUP BY m.month_start
                ORDER BY m.month_start";

            response.MonthlyUsersCreated = (await connection.QueryAsync<DashboardMonthlyTrendPointResponse>(monthlyUsersSql, new
            {
                parameters.OrganizationId,
                parameters.Status,
                parameters.Type,
                Months = months
            })).ToList();

            response.MonthlyNonConformities = new List<DashboardMonthlyTrendPointResponse>();
            response.AlertIndicatorsByOrganization = new List<DashboardChartDataPointResponse>();

            return response;
        }

        public async Task<List<DashboardAlertResponse>> GetAlertsAsync(DashboardQueryParameters query)
        {
            using var connection = _connectionFactory.CreateConnection();
            var parameters = BuildFilters(query);
            var alerts = new List<DashboardAlertResponse>();

            var suspendedSql = @"
                SELECT Id, Name, UpdatedAt
                FROM Organizations
                WHERE Status = 'SUSPENDUE'
                  AND (@OrganizationId IS NULL OR Id = @OrganizationId)
                  AND (@Type IS NULL OR Type = @Type)
                ORDER BY UpdatedAt DESC NULLS LAST
                LIMIT 20";

            var suspended = await connection.QueryAsync<(int Id, string Name, System.DateTime? UpdatedAt)>(suspendedSql, parameters);
            alerts.AddRange(suspended.Select(item => new DashboardAlertResponse
            {
                Type = "ORGANIZATION_SUSPENDED",
                Title = "Organisation suspendue",
                Description = $"{item.Name} est actuellement suspendue.",
                Severity = "HIGH",
                ReferenceId = item.Id.ToString(),
                CreatedAt = item.UpdatedAt ?? System.DateTime.UtcNow
            }));

            var noAdminSql = @"
                SELECT o.Id, o.Name, o.CreatedAt
                FROM Organizations o
                LEFT JOIN Users u ON u.OrganizationId = o.Id AND u.Role = 'ADMIN_ORG' AND u.IsActive = TRUE
                WHERE (@OrganizationId IS NULL OR o.Id = @OrganizationId)
                  AND (@Status IS NULL OR o.Status = @Status)
                  AND (@Type IS NULL OR o.Type = @Type)
                GROUP BY o.Id
                HAVING COUNT(u.Id) = 0
                ORDER BY o.CreatedAt DESC
                LIMIT 20";

            var noAdminOrganizations = await connection.QueryAsync<(int Id, string Name, System.DateTime CreatedAt)>(noAdminSql, parameters);
            alerts.AddRange(noAdminOrganizations.Select(item => new DashboardAlertResponse
            {
                Type = "ORGANIZATION_NO_ADMIN",
                Title = "Organisation sans admin local",
                Description = $"{item.Name} n'a aucun ADMIN_ORG actif.",
                Severity = "MEDIUM",
                ReferenceId = item.Id.ToString(),
                CreatedAt = item.CreatedAt
            }));

            var recentOrgsSql = @"
                SELECT Id, Name, CreatedAt
                FROM Organizations
                WHERE CreatedAt >= NOW() - INTERVAL '7 day'
                  AND (@OrganizationId IS NULL OR Id = @OrganizationId)
                  AND (@Status IS NULL OR Status = @Status)
                  AND (@Type IS NULL OR Type = @Type)
                ORDER BY CreatedAt DESC
                LIMIT 20";

            var recentOrgs = await connection.QueryAsync<(int Id, string Name, System.DateTime CreatedAt)>(recentOrgsSql, parameters);
            alerts.AddRange(recentOrgs.Select(item => new DashboardAlertResponse
            {
                Type = "ORGANIZATION_RECENTLY_CREATED",
                Title = "Nouvelle organisation",
                Description = $"{item.Name} a ete creee recemment.",
                Severity = "INFO",
                ReferenceId = item.Id.ToString(),
                CreatedAt = item.CreatedAt
            }));

            return alerts.OrderByDescending(a => a.CreatedAt).Take(50).ToList();
        }

        public async Task<List<DashboardRecentActivityResponse>> GetRecentActivitiesAsync(DashboardQueryParameters query)
        {
            using var connection = _connectionFactory.CreateConnection();
            var parameters = BuildFilters(query);

            var sql = @"
                SELECT
                    'ORGANIZATION' AS Type,
                    'Organisation creee' AS Title,
                    o.Name AS Description,
                    o.CreatedAt,
                    NULL AS ActorName
                FROM Organizations o
                WHERE (@OrganizationId IS NULL OR o.Id = @OrganizationId)
                  AND (@Status IS NULL OR o.Status = @Status)
                  AND (@Type IS NULL OR o.Type = @Type)
                ORDER BY o.CreatedAt DESC
                LIMIT 30";

            return (await connection.QueryAsync<DashboardRecentActivityResponse>(sql, parameters)).ToList();
        }

        public async Task<List<TopOrganizationResponse>> GetTopOrganizationsAsync(DashboardQueryParameters query, int limit = 10)
        {
            using var connection = _connectionFactory.CreateConnection();
            var parameters = BuildFilters(query);
            parameters.Limit = limit;

            var documentsTableExists = await TableExistsAsync("documents");

            var documentsSubquery = documentsTableExists
                ? "(SELECT COUNT(1) FROM Documents d WHERE d.OrganizationId = o.Id)"
                : "0";

            var sql = $@"
                SELECT
                    o.Id AS OrganizationId,
                    o.Name AS OrganizationName,
                    (SELECT COUNT(1) FROM Users u WHERE u.OrganizationId = o.Id) AS UsersCount,
                    {documentsSubquery} AS DocumentsCount,
                    0 AS NonConformitiesCount
                FROM Organizations o
                WHERE (@OrganizationId IS NULL OR o.Id = @OrganizationId)
                  AND (@Status IS NULL OR o.Status = @Status)
                  AND (@Type IS NULL OR o.Type = @Type)
                ORDER BY UsersCount DESC, DocumentsCount DESC, o.CreatedAt DESC
                LIMIT @Limit";

            return (await connection.QueryAsync<TopOrganizationResponse>(sql, parameters)).ToList();
        }

        private async Task<bool> TableExistsAsync(string tableName)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT to_regclass(@TableName) IS NOT NULL";
            return await connection.QuerySingleAsync<bool>(sql, new { TableName = tableName });
        }

        private static DashboardFilterParams BuildFilters(DashboardQueryParameters query)
        {
            return new DashboardFilterParams
            {
                OrganizationId = query.OrganizationId,
                Status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim().ToUpperInvariant(),
                Type = string.IsNullOrWhiteSpace(query.Type) ? null : query.Type.Trim().ToUpperInvariant()
            };
        }

        private static string BuildOrganizationWhereClause(string alias, bool includeOrganizationId)
        {
            var conditions = new List<string>();

            if (includeOrganizationId)
            {
                conditions.Add($"(@OrganizationId IS NULL OR {alias}.Id = @OrganizationId)");
            }

            conditions.Add($"(@Status IS NULL OR {alias}.Status = @Status)");
            conditions.Add($"(@Type IS NULL OR {alias}.Type = @Type)");

            return $"WHERE {string.Join(" AND ", conditions)}";
        }

        private static string BuildUserWhereClause()
        {
            return @"WHERE (@OrganizationId IS NULL OR u.OrganizationId = @OrganizationId)
                     AND (@Status IS NULL OR o.Status = @Status)
                     AND (@Type IS NULL OR o.Type = @Type)";
        }

        private static string BuildProcessWhereClause()
        {
            return @"WHERE (@OrganizationId IS NULL OR p.OrganizationId = @OrganizationId)
                     AND (@Status IS NULL OR o.Status = @Status)
                     AND (@Type IS NULL OR o.Type = @Type)";
        }

        private static int ParsePeriodInMonths(string? period)
        {
            if (string.IsNullOrWhiteSpace(period))
            {
                return 12;
            }

            var normalized = period.Trim().ToUpperInvariant();
            return normalized switch
            {
                "1M" => 1,
                "3M" => 3,
                "6M" => 6,
                "12M" => 12,
                _ => 12
            };
        }

        private sealed class DashboardFilterParams
        {
            public int? OrganizationId { get; set; }
            public string? Status { get; set; }
            public string? Type { get; set; }
            public int? Limit { get; set; }
        }
    }
}
