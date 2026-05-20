using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class ProcessRepository : IProcessRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ProcessRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<Process>> SearchAsync(
            int pageNumber,
            int pageSize,
            string? search,
            string? type,
            string? status,
            int? pilotUserId,
            int? organizationId,
            int? restrictedUserId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = new DynamicParameters();
            var whereClause = BuildWhereClause(parameters, search, type, status, pilotUserId, organizationId, restrictedUserId);

            parameters.Add("@PageSize", pageSize);
            parameters.Add("@Offset", (pageNumber - 1) * pageSize);

            var sql = $@"
                SELECT *
                FROM Processes
                {whereClause}
                ORDER BY CreatedAt DESC, Id DESC
                LIMIT @PageSize OFFSET @Offset";

            return await connection.QueryAsync<Process>(sql, parameters);
        }

        public async Task<int> CountSearchAsync(
            string? search,
            string? type,
            string? status,
            int? pilotUserId,
            int? organizationId,
            int? restrictedUserId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = new DynamicParameters();
            var whereClause = BuildWhereClause(parameters, search, type, status, pilotUserId, organizationId, restrictedUserId);

            var sql = $@"
                SELECT COUNT(1)
                FROM Processes
                {whereClause}";

            return await connection.QuerySingleAsync<int>(sql, parameters);
        }

        public async Task<Process?> GetByIdAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM Processes WHERE Id = @Id";
            return await connection.QueryFirstOrDefaultAsync<Process>(sql, new { Id = id });
        }

        public async Task<bool> ExistsCodeAsync(int organizationId, string code, int? excludeId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT COUNT(1)
                FROM Processes
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

        public async Task<int> CreateAsync(Process process)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO Processes
                    (OrganizationId, Code, Name, Description, Type, Finalities, Scope, Suppliers, Clients, InputData, OutputData, Objectives, PilotUserId, Status, VersionNumber, RevisionComment, CreatedAt, UpdatedAt)
                VALUES
                    (@OrganizationId, @Code, @Name, @Description, @Type, @Finalities, @Scope, @Suppliers, @Clients, @InputData, @OutputData, @Objectives, @PilotUserId, @Status, @VersionNumber, @RevisionComment, @CreatedAt, @UpdatedAt)
                RETURNING Id;";

            return await connection.QuerySingleAsync<int>(sql, process);
        }

        public async Task<bool> UpdateAsync(Process process)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                UPDATE Processes
                SET Code = @Code,
                    Name = @Name,
                    Description = @Description,
                    Type = @Type,
                    Finalities = @Finalities,
                    Scope = @Scope,
                    Suppliers = @Suppliers,
                    Clients = @Clients,
                    InputData = @InputData,
                    OutputData = @OutputData,
                    Objectives = @Objectives,
                    PilotUserId = @PilotUserId,
                    Status = @Status,
                    VersionNumber = @VersionNumber,
                    RevisionComment = @RevisionComment,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id";

            var rows = await connection.ExecuteAsync(sql, process);
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM Processes WHERE Id = @Id";
            var rows = await connection.ExecuteAsync(sql, new { Id = id });
            return rows > 0;
        }

        public async Task<bool> ToggleStatusAsync(int id, string status)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE Processes
                SET Status = @Status,
                    UpdatedAt = NOW()
                WHERE Id = @Id";

            var rows = await connection.ExecuteAsync(sql, new { Id = id, Status = status });
            return rows > 0;
        }

        public async Task<IEnumerable<Process>> GetByOrganizationAsync(int? organizationId, int? restrictedUserId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            if (restrictedUserId.HasValue)
            {
                const string sql = @"
                    SELECT *
                    FROM Processes
                    WHERE (@OrganizationId IS NULL OR OrganizationId = @OrganizationId)
                      AND (PilotUserId = @RestrictedUserId OR Id IN (SELECT ProcessId FROM ProcessActors WHERE UserId = @RestrictedUserId))
                    ORDER BY Type, Code";

                return await connection.QueryAsync<Process>(sql, new { OrganizationId = organizationId, RestrictedUserId = restrictedUserId.Value });
            }
            else
            {
                const string sql = @"
                    SELECT *
                    FROM Processes
                    WHERE (@OrganizationId IS NULL OR OrganizationId = @OrganizationId)
                    ORDER BY Type, Code";

                return await connection.QueryAsync<Process>(sql, new { OrganizationId = organizationId });
            }
        }

        private static string BuildWhereClause(
            DynamicParameters parameters,
            string? search,
            string? type,
            string? status,
            int? pilotUserId,
            int? organizationId,
            int? restrictedUserId = null)
        {
            var conditions = new List<string>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                conditions.Add("(Code ILIKE @Search OR Name ILIKE @Search OR COALESCE(Description, '') ILIKE @Search)");
                parameters.Add("@Search", $"%{search.Trim()}%");
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                conditions.Add("Type = @Type");
                parameters.Add("@Type", type.Trim());
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                conditions.Add("Status = @Status");
                parameters.Add("@Status", status.Trim());
            }

            if (pilotUserId.HasValue)
            {
                conditions.Add("PilotUserId = @PilotUserId");
                parameters.Add("@PilotUserId", pilotUserId.Value);
            }

            if (organizationId.HasValue)
            {
                conditions.Add("OrganizationId = @OrganizationId");
                parameters.Add("@OrganizationId", organizationId.Value);
            }

            if (restrictedUserId.HasValue)
            {
                conditions.Add("(PilotUserId = @RestrictedUserId OR Id IN (SELECT ProcessId FROM ProcessActors WHERE UserId = @RestrictedUserId))");
                parameters.Add("@RestrictedUserId", restrictedUserId.Value);
            }

            if (!conditions.Any())
            {
                return string.Empty;
            }

            return $"WHERE {string.Join(" AND ", conditions)}";
        }

        public async Task<IEnumerable<Process>> GetByProcedureIdAsync(int procedureId, int? organizationId = null)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT p.*
                FROM Processes p
                INNER JOIN ProcessProcedures pp ON pp.ProcessId = p.Id
                WHERE pp.ProcedureId = @ProcedureId
                  AND (@OrganizationId IS NULL OR p.OrganizationId = @OrganizationId)
                ORDER BY p.CreatedAt DESC, p.Id DESC";
            return await connection.QueryAsync<Process>(sql, new { ProcedureId = procedureId, OrganizationId = organizationId });
        }
    }
}

