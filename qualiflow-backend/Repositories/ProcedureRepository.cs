using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class ProcedureRepository : IProcedureRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ProcedureRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<ProcedureListItemData>> SearchAsync(
            int pageNumber,
            int pageSize,
            string? search,
            int? processId,
            string? status,
            int? responsibleUserId,
            int? organizationId,
            int? restrictedUserId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = new DynamicParameters();
            var whereClause = BuildWhereClause(parameters, search, processId, status, responsibleUserId, organizationId, restrictedUserId);

            parameters.Add("@PageSize", pageSize);
            parameters.Add("@Offset", (pageNumber - 1) * pageSize);

            var sql = $@"
                SELECT
                    pr.Id,
                    pr.OrganizationId,
                    pr.ProcessId,
                    p.Code AS ProcessCode,
                    p.Name AS ProcessName,
                    pr.Code,
                    pr.Title,
                    pr.ResponsibleUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS ResponsibleFullName,
                    pr.Status,
                    pr.VersionNumber,
                    pr.RevisionComment,
                    pr.CreatedAt
                FROM Procedures pr
                INNER JOIN Processes p ON p.Id = pr.ProcessId
                LEFT JOIN Users u ON u.Id = pr.ResponsibleUserId
                {whereClause}
                ORDER BY pr.CreatedAt DESC, pr.Id DESC
                LIMIT @PageSize OFFSET @Offset";

            return await connection.QueryAsync<ProcedureListItemData>(sql, parameters);
        }

        public async Task<int> CountSearchAsync(
            string? search,
            int? processId,
            string? status,
            int? responsibleUserId,
            int? organizationId,
            int? restrictedUserId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = new DynamicParameters();
            var whereClause = BuildWhereClause(parameters, search, processId, status, responsibleUserId, organizationId, restrictedUserId);

            var sql = $@"
                SELECT COUNT(1)
                FROM Procedures pr
                INNER JOIN Processes p ON p.Id = pr.ProcessId
                {whereClause}";

            return await connection.QuerySingleAsync<int>(sql, parameters);
        }

        public async Task<Procedure?> GetByIdAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM Procedures WHERE Id = @Id";
            return await connection.QueryFirstOrDefaultAsync<Procedure>(sql, new { Id = id });
        }

        public async Task<ProcedureListItemData?> GetListItemByIdAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    pr.Id,
                    pr.OrganizationId,
                    pr.ProcessId,
                    p.Code AS ProcessCode,
                    p.Name AS ProcessName,
                    pr.Code,
                    pr.Title,
                    pr.ResponsibleUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS ResponsibleFullName,
                    pr.Status,
                    pr.VersionNumber,
                    pr.RevisionComment,
                    pr.CreatedAt
                FROM Procedures pr
                INNER JOIN Processes p ON p.Id = pr.ProcessId
                LEFT JOIN Users u ON u.Id = pr.ResponsibleUserId
                WHERE pr.Id = @Id";

            return await connection.QueryFirstOrDefaultAsync<ProcedureListItemData>(sql, new { Id = id });
        }

        public async Task<IEnumerable<ProcedureListItemData>> GetByProcessIdAsync(int processId, int? organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    pr.Id,
                    pr.OrganizationId,
                    pr.ProcessId,
                    p.Code AS ProcessCode,
                    p.Name AS ProcessName,
                    pr.Code,
                    pr.Title,
                    pr.ResponsibleUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS ResponsibleFullName,
                    pr.Status,
                    pr.VersionNumber,
                    pr.RevisionComment,
                    pr.CreatedAt
                FROM Procedures pr
                INNER JOIN ProcessProcedures pp ON pp.ProcedureId = pr.Id
                INNER JOIN Processes p ON p.Id = pp.ProcessId
                LEFT JOIN Users u ON u.Id = pr.ResponsibleUserId
                WHERE pp.ProcessId = @ProcessId
                  AND (@OrganizationId IS NULL OR pr.OrganizationId = @OrganizationId)
                ORDER BY pr.CreatedAt DESC, pr.Id DESC";

            return await connection.QueryAsync<ProcedureListItemData>(sql, new
            {
                ProcessId = processId,
                OrganizationId = organizationId
            });
        }

        public async Task<bool> ExistsCodeAsync(int organizationId, string code, int? excludeId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT COUNT(1)
                FROM Procedures
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

        public async Task<int> CreateAsync(Procedure procedure, IEnumerable<int>? additionalProcessIds = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO Procedures
                    (OrganizationId, ProcessId, Code, Title, Objective, Scope, Description, ResponsibleUserId, Status, VersionNumber, RevisionComment, CreatedAt, UpdatedAt)
                VALUES
                    (@OrganizationId, @ProcessId, @Code, @Title, @Objective, @Scope, @Description, @ResponsibleUserId, @Status, @VersionNumber, @RevisionComment, @CreatedAt, @UpdatedAt)
                RETURNING Id;";

            var id = await connection.QuerySingleAsync<int>(sql, procedure);

            const string linkSql = @"
                INSERT INTO ProcessProcedures (ProcessId, ProcedureId)
                VALUES (@ProcessId, @ProcedureId)
                ON CONFLICT (ProcessId, ProcedureId) DO NOTHING;";

            // Always link the primary process
            await connection.ExecuteAsync(linkSql, new { ProcessId = procedure.ProcessId, ProcedureId = id });

            // Link any additional processes
            if (additionalProcessIds != null)
            {
                foreach (var pid in additionalProcessIds.Where(pid => pid != procedure.ProcessId))
                {
                    await connection.ExecuteAsync(linkSql, new { ProcessId = pid, ProcedureId = id });
                }
            }

            return id;
        }

        public async Task<bool> UpdateAsync(Procedure procedure)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                UPDATE Procedures
                SET ProcessId = @ProcessId,
                    Code = @Code,
                    Title = @Title,
                    Objective = @Objective,
                    Scope = @Scope,
                    Description = @Description,
                    ResponsibleUserId = @ResponsibleUserId,
                    Status = @Status,
                    VersionNumber = @VersionNumber,
                    RevisionComment = @RevisionComment,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id";

            var rows = await connection.ExecuteAsync(sql, procedure);
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM Procedures WHERE Id = @Id";
            var rows = await connection.ExecuteAsync(sql, new { Id = id });
            return rows > 0;
        }

        public async Task<bool> ToggleStatusAsync(int id, string status)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE Procedures
                SET Status = @Status,
                    UpdatedAt = NOW()
                WHERE Id = @Id";

            var rows = await connection.ExecuteAsync(sql, new { Id = id, Status = status });
            return rows > 0;
        }

        public async Task<IEnumerable<Procedure>> GetByOrganizationAsync(int? organizationId, int? restrictedUserId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            if (restrictedUserId.HasValue)
            {
                const string sql = @"
                    SELECT pr.*
                    FROM Procedures pr
                    INNER JOIN Processes p ON p.Id = pr.ProcessId
                    WHERE (@OrganizationId IS NULL OR pr.OrganizationId = @OrganizationId)
                      AND (p.PilotUserId = @RestrictedUserId OR pr.ProcessId IN (SELECT ProcessId FROM ProcessActors WHERE UserId = @RestrictedUserId))
                    ORDER BY pr.CreatedAt DESC, pr.Id DESC";

                return await connection.QueryAsync<Procedure>(sql, new { OrganizationId = organizationId, RestrictedUserId = restrictedUserId.Value });
            }
            else
            {
                const string sql = @"
                    SELECT *
                    FROM Procedures
                    WHERE (@OrganizationId IS NULL OR OrganizationId = @OrganizationId)
                    ORDER BY CreatedAt DESC, Id DESC";

                return await connection.QueryAsync<Procedure>(sql, new { OrganizationId = organizationId });
            }
        }

        private static string BuildWhereClause(
            DynamicParameters parameters,
            string? search,
            int? processId,
            string? status,
            int? responsibleUserId,
            int? organizationId,
            int? restrictedUserId = null)
        {
            var conditions = new List<string>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                conditions.Add("(pr.Code ILIKE @Search OR pr.Title ILIKE @Search OR COALESCE(pr.Description, '') ILIKE @Search OR p.Name ILIKE @Search)");
                parameters.Add("@Search", $"%{search.Trim()}%");
            }

            if (processId.HasValue)
            {
                conditions.Add("pr.ProcessId = @ProcessId");
                parameters.Add("@ProcessId", processId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                conditions.Add("pr.Status = @Status");
                parameters.Add("@Status", status.Trim());
            }

            if (responsibleUserId.HasValue)
            {
                conditions.Add("pr.ResponsibleUserId = @ResponsibleUserId");
                parameters.Add("@ResponsibleUserId", responsibleUserId.Value);
            }

            if (organizationId.HasValue)
            {
                conditions.Add("pr.OrganizationId = @OrganizationId");
                parameters.Add("@OrganizationId", organizationId.Value);
            }

            if (restrictedUserId.HasValue)
            {
                conditions.Add("(p.PilotUserId = @RestrictedUserId OR pr.ProcessId IN (SELECT ProcessId FROM ProcessActors WHERE UserId = @RestrictedUserId))");
                parameters.Add("@RestrictedUserId", restrictedUserId.Value);
            }

            if (!conditions.Any())
            {
                return string.Empty;
            }

            return $"WHERE {string.Join(" AND ", conditions)}";
        }

        public async Task<bool> AddProcessLinkAsync(int processId, int procedureId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO ProcessProcedures (ProcessId, ProcedureId)
                VALUES (@ProcessId, @ProcedureId)
                ON CONFLICT (ProcessId, ProcedureId) DO NOTHING;";
            var rows = await connection.ExecuteAsync(sql, new { ProcessId = processId, ProcedureId = procedureId });
            return rows > 0;
        }

        public async Task<bool> RemoveProcessLinkAsync(int processId, int procedureId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                DELETE FROM ProcessProcedures
                WHERE ProcessId = @ProcessId AND ProcedureId = @ProcedureId;";
            var rows = await connection.ExecuteAsync(sql, new { ProcessId = processId, ProcedureId = procedureId });
            return rows > 0;
        }

        public async Task<bool> ClearProcessLinksAsync(int procedureId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM ProcessProcedures WHERE ProcedureId = @ProcedureId;";
            var rows = await connection.ExecuteAsync(sql, new { ProcedureId = procedureId });
            return rows >= 0;
        }

        public async Task<bool> AddDocumentLinkAsync(int procedureId, int documentId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO ProcedureDocuments (ProcedureId, DocumentId)
                VALUES (@ProcedureId, @DocumentId)
                ON CONFLICT (ProcedureId, DocumentId) DO NOTHING;";
            var rows = await connection.ExecuteAsync(sql, new { ProcedureId = procedureId, DocumentId = documentId });
            return rows > 0;
        }

        public async Task<bool> RemoveDocumentLinkAsync(int procedureId, int documentId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                DELETE FROM ProcedureDocuments
                WHERE ProcedureId = @ProcedureId AND DocumentId = @DocumentId;";
            var rows = await connection.ExecuteAsync(sql, new { ProcedureId = procedureId, DocumentId = documentId });
            return rows > 0;
        }

        public async Task<IEnumerable<int>> GetLinkedDocumentIdsAsync(int procedureId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT DocumentId FROM ProcedureDocuments
                WHERE ProcedureId = @ProcedureId;";
            return await connection.QueryAsync<int>(sql, new { ProcedureId = procedureId });
        }
    }
}

