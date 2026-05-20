using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.DTOs.Organizations;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class OrganizationRepository : IOrganizationRepository
    {
        private static readonly HashSet<string> SortableColumns = new(StringComparer.OrdinalIgnoreCase)
        {
            "Name",
            "Code",
            "Type",
            "Status",
            "CreatedAt",
            "UsersCount",
            "AdminsCount"
        };

        private readonly IDbConnectionFactory _connectionFactory;

        public OrganizationRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<Organization?> GetByIdAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM Organizations WHERE Id = @Id";
            return await connection.QueryFirstOrDefaultAsync<Organization>(sql, new { Id = id });
        }

        public async Task<Organization?> GetByCodeAsync(string code)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM Organizations WHERE LOWER(Code) = LOWER(@Code)";
            return await connection.QueryFirstOrDefaultAsync<Organization>(sql, new { Code = code });
        }

        public async Task<Organization?> GetByNameAsync(string name)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM Organizations WHERE LOWER(Name) = LOWER(@Name)";
            return await connection.QueryFirstOrDefaultAsync<Organization>(sql, new { Name = name });
        }

        public async Task<IEnumerable<Organization>> GetAllAsync(int page, int pageSize)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT *
                FROM Organizations
                ORDER BY CreatedAt DESC
                LIMIT @PageSize OFFSET @Offset";

            var offset = (page - 1) * pageSize;
            return await connection.QueryAsync<Organization>(sql, new { Offset = offset, PageSize = pageSize });
        }

        public async Task<int> GetTotalCountAsync()
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT COUNT(1) FROM Organizations";
            return await connection.QuerySingleAsync<int>(sql);
        }

        public async Task<IEnumerable<OrganizationListItem>> SearchAsync(OrganizationListQueryParameters query)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = new DynamicParameters();
            var whereClause = BuildWhereClause(query, parameters, "o");
            var orderClause = BuildOrderClause(query);

            parameters.Add("@PageSize", query.PageSize);
            parameters.Add("@Offset", (query.PageNumber - 1) * query.PageSize);

            var sql = $@"
                SELECT
                    o.Id,
                    o.Name,
                    o.Code,
                    o.Type,
                    o.Status,
                    o.SubscriptionDaysRemaining,
                    o.SubscriptionMonitorEnabled,
                    o.Email,
                    o.Phone,
                    o.LogoPath,
                    o.CreatedAt,
                    COALESCE(COUNT(DISTINCT u.Id), 0) AS UsersCount,
                    COALESCE(COUNT(DISTINCT CASE WHEN u.Role = 'ADMIN_ORG' THEN u.Id END), 0) AS AdminsCount
                FROM Organizations o
                LEFT JOIN Users u ON u.OrganizationId = o.Id
                {whereClause}
                GROUP BY o.Id
                {orderClause}
                LIMIT @PageSize OFFSET @Offset";

            return await connection.QueryAsync<OrganizationListItem>(sql, parameters);
        }

        public async Task<int> CountSearchAsync(OrganizationListQueryParameters query)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = new DynamicParameters();
            var whereClause = BuildWhereClause(query, parameters, "o");

            var sql = $@"
                SELECT COUNT(1)
                FROM Organizations o
                {whereClause}";

            return await connection.QuerySingleAsync<int>(sql, parameters);
        }

        public async Task<OrganizationDetails?> GetDetailsAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    o.Id,
                    o.Name,
                    o.Code,
                    o.Description,
                    o.Type,
                    o.Address,
                    o.Email,
                    o.Phone,
                    o.LogoPath,
                    o.Status,
                    o.SubscriptionDaysRemaining,
                    o.SubscriptionMonitorEnabled,
                    o.CreatedAt,
                    o.UpdatedAt,
                    COALESCE(COUNT(DISTINCT u.Id), 0) AS UsersCount,
                    COALESCE(COUNT(DISTINCT CASE WHEN u.Role = 'ADMIN_ORG' THEN u.Id END), 0) AS AdminsCount
                FROM Organizations o
                LEFT JOIN Users u ON u.OrganizationId = o.Id
                WHERE o.Id = @Id
                GROUP BY o.Id";

            return await connection.QueryFirstOrDefaultAsync<OrganizationDetails>(sql, new { Id = id });
        }

        public async Task<IEnumerable<OrganizationAdmin>> GetAdminsAsync(int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    Id,
                    FirstName,
                    LastName,
                    Email,
                    IsActive,
                    CreatedAt
                FROM Users
                WHERE OrganizationId = @OrganizationId
                  AND Role = 'ADMIN_ORG'
                ORDER BY CreatedAt DESC";

            return await connection.QueryAsync<OrganizationAdmin>(sql, new { OrganizationId = organizationId });
        }

        public async Task<int> CreateAsync(Organization organization)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO Organizations (Name, Code, Description, Type, Address, Email, Phone, LogoPath, Status, SubscriptionDaysRemaining, SubscriptionMonitorEnabled, LastSubscriptionDecrementAt, SubscriptionExpiryAlertSent, CreatedAt)
                VALUES (@Name, @Code, @Description, @Type, @Address, @Email, @Phone, @LogoPath, @Status, @SubscriptionDaysRemaining, @SubscriptionMonitorEnabled, @LastSubscriptionDecrementAt, @SubscriptionExpiryAlertSent, @CreatedAt)
                RETURNING Id;";

            return await connection.QuerySingleAsync<int>(sql, organization);
        }

        public async Task<bool> UpdateAsync(Organization organization)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                UPDATE Organizations
                SET Name = @Name,
                    Description = @Description,
                    Type = @Type,
                    Address = @Address,
                    Email = @Email,
                    Phone = @Phone,
                    LogoPath = @LogoPath,
                    Status = @Status,
                    SubscriptionDaysRemaining = @SubscriptionDaysRemaining,
                    SubscriptionMonitorEnabled = @SubscriptionMonitorEnabled,
                    SubscriptionExpiryAlertSent = @SubscriptionExpiryAlertSent,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id";

            var rowsAffected = await connection.ExecuteAsync(sql, organization);
            return rowsAffected > 0;
        }

        public async Task<bool> UpdateLogoPathAsync(int id, string? logoPath)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                UPDATE Organizations
                SET LogoPath = @LogoPath,
                    UpdatedAt = NOW()
                WHERE Id = @Id";

            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id, LogoPath = logoPath });
            return rowsAffected > 0;
        }

        public async Task<bool> ToggleStatusAsync(int id, string status)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE Organizations SET Status = @Status, UpdatedAt = NOW() WHERE Id = @Id";
            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id, Status = status });
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM Organizations WHERE Id = @Id";
            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
            return rowsAffected > 0;
        }

        public async Task<IReadOnlyList<Organization>> DecrementSubscriptionDaysAsync(DateTime utcNow)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE Organizations
                SET SubscriptionDaysRemaining = GREATEST(SubscriptionDaysRemaining - 1, 0),
                    LastSubscriptionDecrementAt = @UtcNow,
                    UpdatedAt = @UtcNow
                WHERE SubscriptionMonitorEnabled = TRUE
                  AND SubscriptionDaysRemaining > 0
                  AND (
                        LastSubscriptionDecrementAt IS NULL
                        OR DATE(LastSubscriptionDecrementAt) < DATE(@UtcNow)
                  )
                RETURNING *";

            var updated = await connection.QueryAsync<Organization>(sql, new { UtcNow = utcNow });
            return updated.ToList();
        }

        public async Task<IReadOnlyList<Organization>> GetActiveExpiredSubscriptionsAsync()
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT *
                FROM Organizations
                WHERE SubscriptionMonitorEnabled = TRUE
                  AND Status = 'ACTIF'
                  AND SubscriptionDaysRemaining <= 0";

            var organizations = await connection.QueryAsync<Organization>(sql);
            return organizations.ToList();
        }

        public async Task<bool> MarkSubscriptionExpiryAlertSentAsync(int id, bool sent = true)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE Organizations
                SET SubscriptionExpiryAlertSent = @Sent,
                    UpdatedAt = NOW()
                WHERE Id = @Id";

            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id, Sent = sent });
            return rowsAffected > 0;
        }

        private static string BuildWhereClause(OrganizationListQueryParameters query, DynamicParameters parameters, string alias)
        {
            var conditions = new List<string>();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                conditions.Add($"({alias}.Name ILIKE @Search OR {alias}.Code ILIKE @Search OR COALESCE({alias}.Email, '') ILIKE @Search)");
                parameters.Add("@Search", $"%{query.Search.Trim()}%");
            }

            if (!string.IsNullOrWhiteSpace(query.Type))
            {
                conditions.Add($"{alias}.Type = @Type");
                parameters.Add("@Type", query.Type.Trim().ToUpperInvariant());
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                conditions.Add($"{alias}.Status = @Status");
                parameters.Add("@Status", query.Status.Trim().ToUpperInvariant());
            }

            if (!conditions.Any())
            {
                return string.Empty;
            }

            return $"WHERE {string.Join(" AND ", conditions)}";
        }

        private static string BuildOrderClause(OrganizationListQueryParameters query)
        {
            var sortBy = string.IsNullOrWhiteSpace(query.SortBy) ? "CreatedAt" : query.SortBy.Trim();
            if (!SortableColumns.Contains(sortBy))
            {
                sortBy = "CreatedAt";
            }

            var direction = query.SortDirection?.Trim().ToUpperInvariant() == "ASC" ? "ASC" : "DESC";
            return $"ORDER BY {sortBy} {direction}";
        }
    }
}
