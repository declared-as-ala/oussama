using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class NonConformityRepository : INonConformityRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public NonConformityRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<NonConformityListItemData>> SearchAsync(
            int pageNumber,
            int pageSize,
            string? search,
            string? status,
            string? severity,
            int? processId,
            int? responsibleUserId,
            int? organizationId,
            int? restrictedUserId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = new DynamicParameters();
            var whereClause = BuildWhereClause(parameters, search, status, severity, processId, responsibleUserId, organizationId, restrictedUserId);

            parameters.Add("@PageSize", pageSize);
            parameters.Add("@Offset", (pageNumber - 1) * pageSize);

            var sql = $@"
                SELECT
                    nc.Id,
                    nc.OrganizationId,
                    nc.Code,
                    nc.Title,
                    nc.Type,
                    nc.Severity,
                    nc.ProcessId,
                    p.Code AS ProcessCode,
                    nc.ProcedureId,
                    pr.Code AS ProcedureCode,
                    nc.ResponsibleUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS ResponsibleFullName,
                    nc.DetectedDate,
                    nc.Status,
                    nc.CreatedAt
                FROM NonConformities nc
                LEFT JOIN Processes p ON p.Id = nc.ProcessId
                LEFT JOIN Procedures pr ON pr.Id = nc.ProcedureId
                LEFT JOIN Users u ON u.Id = nc.ResponsibleUserId
                {whereClause}
                ORDER BY nc.DetectedDate DESC, nc.Id DESC
                LIMIT @PageSize OFFSET @Offset";

            return await connection.QueryAsync<NonConformityListItemData>(sql, parameters);
        }

        public async Task<int> CountSearchAsync(
            string? search,
            string? status,
            string? severity,
            int? processId,
            int? responsibleUserId,
            int? organizationId,
            int? restrictedUserId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = new DynamicParameters();
            var whereClause = BuildWhereClause(parameters, search, status, severity, processId, responsibleUserId, organizationId, restrictedUserId);

            var sql = $@"
                SELECT COUNT(1)
                FROM NonConformities nc
                LEFT JOIN Processes p ON p.Id = nc.ProcessId
                LEFT JOIN Procedures pr ON pr.Id = nc.ProcedureId
                LEFT JOIN Users u ON u.Id = nc.ResponsibleUserId
                {whereClause}";

            return await connection.QuerySingleAsync<int>(sql, parameters);
        }

        public async Task<NonConformity?> GetByIdAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM NonConformities WHERE Id = @Id";
            return await connection.QueryFirstOrDefaultAsync<NonConformity>(sql, new { Id = id });
        }

        public async Task<NonConformityListItemData?> GetListItemByIdAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    nc.Id,
                    nc.OrganizationId,
                    nc.Code,
                    nc.Title,
                    nc.Type,
                    nc.Severity,
                    nc.ProcessId,
                    p.Code AS ProcessCode,
                    nc.ProcedureId,
                    pr.Code AS ProcedureCode,
                    nc.ResponsibleUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS ResponsibleFullName,
                    nc.DetectedDate,
                    nc.Status,
                    nc.CreatedAt
                FROM NonConformities nc
                LEFT JOIN Processes p ON p.Id = nc.ProcessId
                LEFT JOIN Procedures pr ON pr.Id = nc.ProcedureId
                LEFT JOIN Users u ON u.Id = nc.ResponsibleUserId
                WHERE nc.Id = @Id";

            return await connection.QueryFirstOrDefaultAsync<NonConformityListItemData>(sql, new { Id = id });
        }

        public async Task<bool> ExistsCodeAsync(int organizationId, string code, int? excludeId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT COUNT(1)
                FROM NonConformities
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

        public async Task<string> GenerateNextCodeAsync(int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT COALESCE(
                    MAX(CAST(SUBSTRING(Code, 4) AS INTEGER)),
                    0
                ) + 1
                FROM NonConformities
                WHERE OrganizationId = @OrganizationId
                  AND Code LIKE 'NC-%'";

            var nextNumber = await connection.QuerySingleAsync<int>(sql, new { OrganizationId = organizationId });
            return $"NC-{nextNumber:D3}";
        }

        public async Task<int> CreateAsync(NonConformity nonConformity)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO NonConformities
                    (OrganizationId, Code, Title, Description, Type, Severity, ProcessId, ProcedureId, DetectedDate, ResponsibleUserId, Status, CreatedAt, UpdatedAt)
                VALUES
                    (@OrganizationId, @Code, @Title, @Description, @Type, @Severity, @ProcessId, @ProcedureId, @DetectedDate, @ResponsibleUserId, @Status, @CreatedAt, @UpdatedAt)
                RETURNING Id;";

            return await connection.QuerySingleAsync<int>(sql, nonConformity);
        }

        public async Task<bool> UpdateAsync(NonConformity nonConformity)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                UPDATE NonConformities
                SET Code = @Code,
                    Title = @Title,
                    Description = @Description,
                    Type = @Type,
                    Severity = @Severity,
                    ProcessId = @ProcessId,
                    ProcedureId = @ProcedureId,
                    DetectedDate = @DetectedDate,
                    ResponsibleUserId = @ResponsibleUserId,
                    Status = @Status,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id";

            var rows = await connection.ExecuteAsync(sql, nonConformity);
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM NonConformities WHERE Id = @Id";
            var rows = await connection.ExecuteAsync(sql, new { Id = id });
            return rows > 0;
        }

        public async Task<bool> UpdateStatusAsync(int id, string status)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE NonConformities
                SET Status = @Status,
                    UpdatedAt = NOW()
                WHERE Id = @Id";

            var rows = await connection.ExecuteAsync(sql, new { Id = id, Status = status });
            return rows > 0;
        }

        public async Task<bool> ValidateAsync(int id, string code, int responsibleUserId, string status)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE NonConformities
                SET Code = @Code,
                    ResponsibleUserId = @ResponsibleUserId,
                    Status = @Status,
                    UpdatedAt = NOW()
                WHERE Id = @Id";

            var rows = await connection.ExecuteAsync(sql, new
            {
                Id = id,
                Code = code,
                ResponsibleUserId = responsibleUserId,
                Status = status
            });
            return rows > 0;
        }

        public async Task<IEnumerable<NonConformity>> GetByOrganizationAsync(int? organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT *
                FROM NonConformities
                WHERE (@OrganizationId IS NULL OR OrganizationId = @OrganizationId)
                ORDER BY DetectedDate DESC, Id DESC";

            return await connection.QueryAsync<NonConformity>(sql, new { OrganizationId = organizationId });
        }

        public async Task<IEnumerable<NonConformityListItemData>> GetAwaitingValidationAsync(int organizationId, int pageNumber, int pageSize)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = new DynamicParameters();
            parameters.Add("@OrganizationId", organizationId);
            parameters.Add("@Status", NonConformityConstants.StatusEnAttenteValidation);
            parameters.Add("@PageSize", pageSize);
            parameters.Add("@Offset", (pageNumber - 1) * pageSize);

            const string sql = @"
                SELECT
                    nc.Id,
                    nc.OrganizationId,
                    nc.Code,
                    nc.Title,
                    nc.Type,
                    nc.Severity,
                    nc.ProcessId,
                    p.Code AS ProcessCode,
                    nc.ProcedureId,
                    pr.Code AS ProcedureCode,
                    nc.ResponsibleUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS ResponsibleFullName,
                    nc.DetectedDate,
                    nc.Status,
                    nc.CreatedAt
                FROM NonConformities nc
                LEFT JOIN Processes p ON p.Id = nc.ProcessId
                LEFT JOIN Procedures pr ON pr.Id = nc.ProcedureId
                LEFT JOIN Users u ON u.Id = nc.ResponsibleUserId
                WHERE nc.OrganizationId = @OrganizationId
                  AND nc.Status = @Status
                ORDER BY nc.DetectedDate DESC, nc.Id DESC
                LIMIT @PageSize OFFSET @Offset";

            return await connection.QueryAsync<NonConformityListItemData>(sql, parameters);
        }

        public async Task<int> CountAwaitingValidationAsync(int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT COUNT(1)
                FROM NonConformities
                WHERE OrganizationId = @OrganizationId
                  AND Status = @Status";

            return await connection.QuerySingleAsync<int>(sql, new
            {
                OrganizationId = organizationId,
                Status = NonConformityConstants.StatusEnAttenteValidation
            });
        }

        private static string BuildWhereClause(
            DynamicParameters parameters,
            string? search,
            string? status,
            string? severity,
            int? processId,
            int? responsibleUserId,
            int? organizationId,
            int? restrictedUserId = null)
        {
            var conditions = new List<string>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                conditions.Add(@"(
                    nc.Code ILIKE @Search
                    OR nc.Title ILIKE @Search
                    OR COALESCE(nc.Description, '') ILIKE @Search
                    OR COALESCE(p.Code, '') ILIKE @Search
                    OR COALESCE(pr.Code, '') ILIKE @Search
                    OR COALESCE(u.FirstName, '') ILIKE @Search
                    OR COALESCE(u.LastName, '') ILIKE @Search
                )");
                parameters.Add("@Search", $"%{search.Trim()}%");
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                conditions.Add("nc.Status = @Status");
                parameters.Add("@Status", status.Trim());
            }

            if (!string.IsNullOrWhiteSpace(severity))
            {
                conditions.Add("nc.Severity = @Severity");
                parameters.Add("@Severity", severity.Trim());
            }

            if (processId.HasValue)
            {
                conditions.Add("nc.ProcessId = @ProcessId");
                parameters.Add("@ProcessId", processId.Value);
            }

            if (responsibleUserId.HasValue)
            {
                conditions.Add("nc.ResponsibleUserId = @ResponsibleUserId");
                parameters.Add("@ResponsibleUserId", responsibleUserId.Value);
            }

            if (organizationId.HasValue)
            {
                conditions.Add("nc.OrganizationId = @OrganizationId");
                parameters.Add("@OrganizationId", organizationId.Value);
            }

            if (restrictedUserId.HasValue)
            {
                conditions.Add(@"nc.ProcessId IN (
                    SELECT Id FROM Processes WHERE PilotUserId = @RestrictedUserId
                    UNION
                    SELECT ProcessId FROM ProcessActors WHERE UserId = @RestrictedUserId
                )");
                parameters.Add("@RestrictedUserId", restrictedUserId.Value);
            }

            if (!conditions.Any())
            {
                return string.Empty;
            }

            return $"WHERE {string.Join(" AND ", conditions)}";
        }

        public async Task<int> AddAttachmentAsync(NonConformityAttachment attachment)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO NonConformityAttachments
                    (NonConformityId, OrganizationId, FileName, OriginalFileName, FileExtension, MimeType, FileSize, FileContent, CreatedAt)
                VALUES
                    (@NonConformityId, @OrganizationId, @FileName, @OriginalFileName, @FileExtension, @MimeType, @FileSize, @FileContent, @CreatedAt)
                RETURNING Id;";
            return await connection.QuerySingleAsync<int>(sql, attachment);
        }

        public async Task<NonConformityAttachment?> GetAttachmentByIdAsync(int attachmentId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM NonConformityAttachments WHERE Id = @Id;";
            return await connection.QueryFirstOrDefaultAsync<NonConformityAttachment>(sql, new { Id = attachmentId });
        }

        public async Task<IEnumerable<NonConformityAttachment>> GetAttachmentsByNonConformityIdAsync(int nonConformityId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM NonConformityAttachments WHERE NonConformityId = @NonConformityId ORDER BY CreatedAt DESC;";
            return await connection.QueryAsync<NonConformityAttachment>(sql, new { NonConformityId = nonConformityId });
        }

        public async Task<bool> DeleteAttachmentAsync(int attachmentId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM NonConformityAttachments WHERE Id = @Id;";
            var rows = await connection.ExecuteAsync(sql, new { Id = attachmentId });
            return rows > 0;
        }
    }
}
