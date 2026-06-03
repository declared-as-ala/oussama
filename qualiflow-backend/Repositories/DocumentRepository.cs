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
    public class DocumentRepository : IDocumentRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public DocumentRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<DocumentListItemData>> SearchAsync(
            int pageNumber,
            int pageSize,
            string? search,
            string? type,
            string? status,
            int? processId,
            int? procedureId,
            int? ownerUserId,
            int? organizationId,
            bool pendingValidationOnly,
            bool hidePendingValidationFromGlobal,
            int? restrictedUserId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = new DynamicParameters();
            var whereClause = BuildWhereClause(
                parameters,
                search,
                type,
                status,
                processId,
                procedureId,
                ownerUserId,
                organizationId,
                pendingValidationOnly,
                hidePendingValidationFromGlobal,
                restrictedUserId);

            parameters.Add("@PageSize", pageSize);
            parameters.Add("@Offset", (pageNumber - 1) * pageSize);

            var sql = $@"
                SELECT
                    d.Id,
                    d.OrganizationId,
                    d.Code,
                    d.Title,
                    d.Type,
                    d.ProcessId,
                    p.Code AS ProcessCode,
                    p.Name AS ProcessName,
                    d.ProcedureId,
                    pr.Code AS ProcedureCode,
                    d.OwnerUserId,
                    NULLIF(TRIM(COALESCE(ou.FirstName, '') || ' ' || COALESCE(ou.LastName, '')), '') AS OwnerFullName,
                    d.IsActive,
                    COALESCE(cv.Status, 'BROUILLON') AS Status,
                    cv.VersionNumber,
                    cv.OriginalFileName AS FileName,
                    cv.ExpiryDate,
                    d.DeletedAt,
                    d.CreatedAt,
                    COALESCE(cv.UpdatedAt, cv.CreatedAt, d.UpdatedAt, d.CreatedAt) AS UpdatedAt
                FROM Documents d
                LEFT JOIN Processes p ON p.Id = d.ProcessId
                LEFT JOIN Procedures pr ON pr.Id = d.ProcedureId
                LEFT JOIN Users ou ON ou.Id = d.OwnerUserId
                LEFT JOIN LATERAL (
                    SELECT dv.*
                    FROM DocumentVersions dv
                    WHERE dv.DocumentId = d.Id
                    ORDER BY dv.IsCurrent DESC, dv.CreatedAt DESC, dv.Id DESC
                    LIMIT 1
                ) cv ON TRUE
                LEFT JOIN Users eu ON eu.Id = cv.EstablishedByUserId
                {whereClause}
                ORDER BY COALESCE(cv.UpdatedAt, cv.CreatedAt, d.UpdatedAt, d.CreatedAt) DESC, d.Id DESC
                LIMIT @PageSize OFFSET @Offset;";

            return await connection.QueryAsync<DocumentListItemData>(sql, parameters);
        }

        public async Task<int> CountSearchAsync(
            string? search,
            string? type,
            string? status,
            int? processId,
            int? procedureId,
            int? ownerUserId,
            int? organizationId,
            bool pendingValidationOnly,
            bool hidePendingValidationFromGlobal,
            int? restrictedUserId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            var parameters = new DynamicParameters();
            var whereClause = BuildWhereClause(
                parameters,
                search,
                type,
                status,
                processId,
                procedureId,
                ownerUserId,
                organizationId,
                pendingValidationOnly,
                hidePendingValidationFromGlobal,
                restrictedUserId);

            var sql = $@"
                SELECT COUNT(1)
                FROM Documents d
                LEFT JOIN Processes p ON p.Id = d.ProcessId
                LEFT JOIN Procedures pr ON pr.Id = d.ProcedureId
                LEFT JOIN LATERAL (
                    SELECT dv.*
                    FROM DocumentVersions dv
                    WHERE dv.DocumentId = d.Id
                    ORDER BY dv.IsCurrent DESC, dv.CreatedAt DESC, dv.Id DESC
                    LIMIT 1
                ) cv ON TRUE
                LEFT JOIN Users eu ON eu.Id = cv.EstablishedByUserId
                {whereClause};";

            return await connection.QuerySingleAsync<int>(sql, parameters);
        }

        public async Task<Document?> GetByIdAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM Documents WHERE Id = @Id AND DeletedAt IS NULL";
            return await connection.QueryFirstOrDefaultAsync<Document>(sql, new { Id = id });
        }

        public async Task<Document?> GetByIdIncludingDeletedAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM Documents WHERE Id = @Id";
            return await connection.QueryFirstOrDefaultAsync<Document>(sql, new { Id = id });
        }

        public async Task<DocumentDetailsData?> GetDetailsByIdAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection.State != System.Data.ConnectionState.Open) await ((System.Data.Common.DbConnection)connection).OpenAsync();

            const string sql = @"
                SELECT
                    d.Id,
                    d.OrganizationId,
                    d.ProcessId,
                    p.Code AS ProcessCode,
                    p.Name AS ProcessName,
                    d.ProcedureId,
                    pr.Code AS ProcedureCode,
                    pr.Title AS ProcedureTitle,
                    d.Code,
                    d.Title,
                    d.Type,
                    d.Description,
                    d.Category,
                    d.Keywords,
                    d.Signature,
                    d.OwnerUserId,
                    NULLIF(TRIM(COALESCE(ou.FirstName, '') || ' ' || COALESCE(ou.LastName, '')), '') AS OwnerFullName,
                    d.CurrentVersionId,
                    d.IsActive,
                    d.CreatedAt,
                    d.UpdatedAt
                FROM Documents d
                LEFT JOIN Processes p ON p.Id = d.ProcessId
                LEFT JOIN Procedures pr ON pr.Id = d.ProcedureId
                LEFT JOIN Users ou ON ou.Id = d.OwnerUserId
                WHERE d.Id = @Id AND d.DeletedAt IS NULL;";

            return await connection.QueryFirstOrDefaultAsync<DocumentDetailsData>(sql, new { Id = id });
        }

        public async Task<DocumentListItemData?> GetListItemByIdAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    d.Id,
                    d.OrganizationId,
                    d.Code,
                    d.Title,
                    d.Type,
                    d.ProcessId,
                    p.Code AS ProcessCode,
                    p.Name AS ProcessName,
                    d.ProcedureId,
                    pr.Code AS ProcedureCode,
                    d.OwnerUserId,
                    NULLIF(TRIM(COALESCE(ou.FirstName, '') || ' ' || COALESCE(ou.LastName, '')), '') AS OwnerFullName,
                    d.IsActive,
                    COALESCE(cv.Status, 'BROUILLON') AS Status,
                    cv.VersionNumber,
                    cv.OriginalFileName AS FileName,
                    cv.ExpiryDate,
                    d.DeletedAt,
                    d.CreatedAt,
                    COALESCE(cv.UpdatedAt, cv.CreatedAt, d.UpdatedAt, d.CreatedAt) AS UpdatedAt
                FROM Documents d
                LEFT JOIN Processes p ON p.Id = d.ProcessId
                LEFT JOIN Procedures pr ON pr.Id = d.ProcedureId
                LEFT JOIN Users ou ON ou.Id = d.OwnerUserId
                LEFT JOIN LATERAL (
                    SELECT dv.*
                    FROM DocumentVersions dv
                    WHERE dv.DocumentId = d.Id
                    ORDER BY dv.IsCurrent DESC, dv.CreatedAt DESC, dv.Id DESC
                    LIMIT 1
                ) cv ON TRUE
                WHERE d.Id = @Id AND d.DeletedAt IS NULL;";

            return await connection.QueryFirstOrDefaultAsync<DocumentListItemData>(sql, new { Id = id });
        }

        public async Task<IEnumerable<DocumentListItemData>> GetDeletedAsync(int pageNumber, int pageSize, int organizationId, int? restrictedUserId = null)
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection.State != System.Data.ConnectionState.Open) await ((System.Data.Common.DbConnection)connection).OpenAsync();

            const string sql = @"
                SELECT
                    d.Id,
                    d.OrganizationId,
                    d.Code,
                    d.Title,
                    d.Type,
                    d.ProcessId,
                    p.Code AS ProcessCode,
                    p.Name AS ProcessName,
                    d.ProcedureId,
                    pr.Code AS ProcedureCode,
                    d.OwnerUserId,
                    NULLIF(TRIM(COALESCE(ou.FirstName, '') || ' ' || COALESCE(ou.LastName, '')), '') AS OwnerFullName,
                    d.IsActive,
                    COALESCE(cv.Status, 'BROUILLON') AS Status,
                    cv.VersionNumber,
                    cv.OriginalFileName AS FileName,
                    cv.ExpiryDate,
                    d.DeletedAt,
                    d.CreatedAt,
                    COALESCE(d.DeletedAt, d.UpdatedAt, d.CreatedAt) AS UpdatedAt
                FROM Documents d
                LEFT JOIN Processes p ON p.Id = d.ProcessId
                LEFT JOIN Procedures pr ON pr.Id = d.ProcedureId
                LEFT JOIN Users ou ON ou.Id = d.OwnerUserId
                LEFT JOIN LATERAL (
                    SELECT dv.*
                    FROM DocumentVersions dv
                    WHERE dv.DocumentId = d.Id
                    ORDER BY dv.IsCurrent DESC, dv.CreatedAt DESC, dv.Id DESC
                    LIMIT 1
                ) cv ON TRUE
                WHERE d.OrganizationId = @OrganizationId
                  AND d.DeletedAt IS NOT NULL
                  AND (@RestrictedUserId IS NULL OR d.ProcessId IS NULL OR p.PilotUserId = @RestrictedUserId OR d.ProcessId IN (SELECT ProcessId FROM ProcessActors WHERE UserId = @RestrictedUserId))
                ORDER BY d.DeletedAt DESC, d.Id DESC
                LIMIT @PageSize OFFSET @Offset;";

            return await connection.QueryAsync<DocumentListItemData>(sql, new
            {
                OrganizationId = organizationId,
                RestrictedUserId = restrictedUserId,
                PageSize = pageSize,
                Offset = (pageNumber - 1) * pageSize
            });
        }

        public async Task<int> CountDeletedAsync(int organizationId, int? restrictedUserId = null)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT COUNT(1)
                FROM Documents d
                LEFT JOIN Processes p ON p.Id = d.ProcessId
                WHERE d.OrganizationId = @OrganizationId
                  AND d.DeletedAt IS NOT NULL
                  AND (@RestrictedUserId IS NULL OR d.ProcessId IS NULL OR p.PilotUserId = @RestrictedUserId OR d.ProcessId IN (SELECT ProcessId FROM ProcessActors WHERE UserId = @RestrictedUserId));";

            return await connection.QuerySingleAsync<int>(sql, new { OrganizationId = organizationId, RestrictedUserId = restrictedUserId });
        }

        public async Task<bool> ExistsCodeAsync(int organizationId, string code, int? excludeId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT COUNT(1)
                FROM Documents
                WHERE OrganizationId = @OrganizationId
                  AND LOWER(Code) = LOWER(@Code)
                  AND DeletedAt IS NULL
                  AND (@ExcludeId IS NULL OR Id <> @ExcludeId);";

            var count = await connection.QuerySingleAsync<int>(sql, new
            {
                OrganizationId = organizationId,
                Code = code,
                ExcludeId = excludeId
            });

            return count > 0;
        }

        public async Task<int> CreateAsync(Document document)
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection.State != System.Data.ConnectionState.Open) await ((System.Data.Common.DbConnection)connection).OpenAsync();

            using var transaction = connection.BeginTransaction();
            try
            {
                const string sql = @"
                    INSERT INTO Documents
                        (OrganizationId, ProcessId, ProcedureId, Code, Title, Type, Description, Category, Keywords, Signature, OwnerUserId, CurrentVersionId, IsActive, DeletedAt, CreatedAt, UpdatedAt)
                    VALUES
                        (@OrganizationId, @ProcessId, @ProcedureId, @Code, @Title, @Type, @Description, @Category, @Keywords, @Signature, @OwnerUserId, @CurrentVersionId, @IsActive, @DeletedAt, @CreatedAt, @UpdatedAt)
                    RETURNING Id;";

                var id = await connection.QuerySingleAsync<int>(sql, document, transaction);

                if (document.ProcessIds != null && document.ProcessIds.Any())
                {
                    const string processSql = "INSERT INTO DocumentProcesses (DocumentId, ProcessId) VALUES (@DocumentId, @ProcessId) ON CONFLICT DO NOTHING;";
                    foreach (var processId in document.ProcessIds)
                    {
                        await connection.ExecuteAsync(processSql, new { DocumentId = id, ProcessId = processId }, transaction);
                    }
                }

                if (document.ProcedureIds != null && document.ProcedureIds.Any())
                {
                    const string procedureSql = "INSERT INTO DocumentProcedures (DocumentId, ProcedureId) VALUES (@DocumentId, @ProcedureId) ON CONFLICT DO NOTHING;";
                    foreach (var procedureId in document.ProcedureIds)
                    {
                        await connection.ExecuteAsync(procedureSql, new { DocumentId = id, ProcedureId = procedureId }, transaction);
                    }
                }

                transaction.Commit();
                return id;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> UpdateAsync(Document document)
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection.State != System.Data.ConnectionState.Open) await ((System.Data.Common.DbConnection)connection).OpenAsync();

            using var transaction = connection.BeginTransaction();
            try
            {
                const string sql = @"
                    UPDATE Documents
                    SET ProcessId = @ProcessId,
                        ProcedureId = @ProcedureId,
                        Code = @Code,
                        Title = @Title,
                        Type = @Type,
                        Description = @Description,
                        Category = @Category,
                        Keywords = @Keywords,
                        Signature = @Signature,
                        OwnerUserId = @OwnerUserId,
                        IsActive = @IsActive,
                        UpdatedAt = @UpdatedAt
                    WHERE Id = @Id;";

                var rows = await connection.ExecuteAsync(sql, document, transaction);

                // Sync ProcessIds
                await connection.ExecuteAsync("DELETE FROM DocumentProcesses WHERE DocumentId = @DocumentId;", new { DocumentId = document.Id }, transaction);
                if (document.ProcessIds != null && document.ProcessIds.Any())
                {
                    const string processSql = "INSERT INTO DocumentProcesses (DocumentId, ProcessId) VALUES (@DocumentId, @ProcessId) ON CONFLICT DO NOTHING;";
                    foreach (var processId in document.ProcessIds)
                    {
                        await connection.ExecuteAsync(processSql, new { DocumentId = document.Id, ProcessId = processId }, transaction);
                    }
                }

                // Sync ProcedureIds
                await connection.ExecuteAsync("DELETE FROM DocumentProcedures WHERE DocumentId = @DocumentId;", new { DocumentId = document.Id }, transaction);
                if (document.ProcedureIds != null && document.ProcedureIds.Any())
                {
                    const string procedureSql = "INSERT INTO DocumentProcedures (DocumentId, ProcedureId) VALUES (@DocumentId, @ProcedureId) ON CONFLICT DO NOTHING;";
                    foreach (var procedureId in document.ProcedureIds)
                    {
                        await connection.ExecuteAsync(procedureSql, new { DocumentId = document.Id, ProcedureId = procedureId }, transaction);
                    }
                }

                transaction.Commit();
                return rows > 0;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> SoftDeleteAsync(int id, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection.State != System.Data.ConnectionState.Open) await ((System.Data.Common.DbConnection)connection).OpenAsync();
            const string sql = @"
                UPDATE Documents
                SET IsActive = FALSE,
                    DeletedAt = NOW(),
                    UpdatedAt = NOW()
                WHERE Id = @Id
                  AND OrganizationId = @OrganizationId
                  AND DeletedAt IS NULL;";

            var rows = await connection.ExecuteAsync(sql, new { Id = id, OrganizationId = organizationId });
            return rows > 0;
        }

        public async Task<bool> RestoreAsync(int id, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection.State != System.Data.ConnectionState.Open) await ((System.Data.Common.DbConnection)connection).OpenAsync();
            const string sql = @"
                UPDATE Documents
                SET IsActive = TRUE,
                    DeletedAt = NULL,
                    UpdatedAt = NOW()
                WHERE Id = @Id
                  AND OrganizationId = @OrganizationId
                  AND DeletedAt IS NOT NULL;";

            var rows = await connection.ExecuteAsync(sql, new { Id = id, OrganizationId = organizationId });
            return rows > 0;
        }

        public async Task<bool> PermanentDeleteAsync(int id, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection.State != System.Data.ConnectionState.Open) await ((System.Data.Common.DbConnection)connection).OpenAsync();
            const string sql = @"
                DELETE FROM Documents
                WHERE Id = @Id
                  AND OrganizationId = @OrganizationId
                  AND DeletedAt IS NOT NULL;";

            var rows = await connection.ExecuteAsync(sql, new { Id = id, OrganizationId = organizationId });
            return rows > 0;
        }

        public async Task<int> PurgeExpiredDeletedAsync(int organizationId, DateTime cutoffUtc)
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection.State != System.Data.ConnectionState.Open) await ((System.Data.Common.DbConnection)connection).OpenAsync();
            const string sql = @"
                DELETE FROM Documents
                WHERE OrganizationId = @OrganizationId
                  AND DeletedAt IS NOT NULL
                  AND DeletedAt <= @CutoffUtc;";

            return await connection.ExecuteAsync(sql, new { OrganizationId = organizationId, CutoffUtc = cutoffUtc });
        }

        public async Task<bool> SetActiveAsync(int id, bool isActive)
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection.State != System.Data.ConnectionState.Open) await ((System.Data.Common.DbConnection)connection).OpenAsync();
            const string sql = @"
                UPDATE Documents
                SET IsActive = @IsActive,
                    UpdatedAt = NOW()
                WHERE Id = @Id;";

            var rows = await connection.ExecuteAsync(sql, new { Id = id, IsActive = isActive });
            return rows > 0;
        }

        public async Task<bool> SetCurrentVersionAsync(int documentId, int? currentVersionId)
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection.State != System.Data.ConnectionState.Open) await ((System.Data.Common.DbConnection)connection).OpenAsync();
            const string sql = @"
                UPDATE Documents
                SET CurrentVersionId = @CurrentVersionId,
                    UpdatedAt = NOW()
                WHERE Id = @DocumentId;";

            var rows = await connection.ExecuteAsync(sql, new { DocumentId = documentId, CurrentVersionId = currentVersionId });
            return rows > 0;
        }

        public async Task<IEnumerable<Document>> GetByOrganizationAsync(int? organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection.State != System.Data.ConnectionState.Open) await ((System.Data.Common.DbConnection)connection).OpenAsync();

            const string sql = @"
                SELECT *
                FROM Documents
                WHERE (@OrganizationId IS NULL OR OrganizationId = @OrganizationId)
                  AND IsActive = TRUE
                  AND DeletedAt IS NULL
                ORDER BY CreatedAt DESC, Id DESC;";

            return await connection.QueryAsync<Document>(sql, new { OrganizationId = organizationId });
        }

        public async Task<IEnumerable<Document>> GetByIdsAsync(int organizationId, IEnumerable<int> ids)
        {
            var idArray = ids?.Distinct().ToArray() ?? Array.Empty<int>();
            if (idArray.Length == 0)
            {
                return Array.Empty<Document>();
            }

            using var connection = _connectionFactory.CreateConnection();
            if (connection.State != System.Data.ConnectionState.Open) await ((System.Data.Common.DbConnection)connection).OpenAsync();

            const string sql = @"
                SELECT *
                FROM Documents
                WHERE OrganizationId = @OrganizationId
                  AND Id = ANY(@Ids)
                  AND IsActive = TRUE
                  AND DeletedAt IS NULL
                ORDER BY Id ASC;";

            return await connection.QueryAsync<Document>(sql, new { OrganizationId = organizationId, Ids = idArray });
        }

        public async Task<IEnumerable<DocumentExpiringData>> GetExpiringAsync(int organizationId, int withinDays)
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection.State != System.Data.ConnectionState.Open) await ((System.Data.Common.DbConnection)connection).OpenAsync();

            const string sql = @"
                SELECT
                    d.Id,
                    d.OrganizationId,
                    d.Code,
                    d.Title,
                    COALESCE(cv.Status, 'BROUILLON') AS Status,
                    cv.VersionNumber,
                    cv.ExpiryDate,
                    (cv.ExpiryDate::date - CURRENT_DATE) AS DaysToExpiry,
                    d.OwnerUserId,
                    NULLIF(TRIM(COALESCE(ou.FirstName, '') || ' ' || COALESCE(ou.LastName, '')), '') AS OwnerFullName
                FROM Documents d
                INNER JOIN DocumentVersions cv ON cv.Id = d.CurrentVersionId
                LEFT JOIN Users ou ON ou.Id = d.OwnerUserId
                WHERE d.OrganizationId = @OrganizationId
                  AND d.IsActive = TRUE
                  AND d.DeletedAt IS NULL
                  AND cv.ExpiryDate IS NOT NULL
                  AND COALESCE(cv.Status, 'BROUILLON') <> 'ARCHIVE'
                  AND cv.ExpiryDate::date <= (CURRENT_DATE + @WithinDays * INTERVAL '1 day')
                ORDER BY cv.ExpiryDate ASC, d.Id ASC;";

            return await connection.QueryAsync<DocumentExpiringData>(sql, new
            {
                OrganizationId = organizationId,
                WithinDays = withinDays
            });
        }

        public async Task<IEnumerable<int>> GetProcessIdsByDocumentIdAsync(int documentId)
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection.State != System.Data.ConnectionState.Open) await ((System.Data.Common.DbConnection)connection).OpenAsync();
            const string sql = "SELECT ProcessId FROM DocumentProcesses WHERE DocumentId = @DocumentId;";
            return await connection.QueryAsync<int>(sql, new { DocumentId = documentId });
        }

        public async Task<IEnumerable<int>> GetProcedureIdsByDocumentIdAsync(int documentId)
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection.State != System.Data.ConnectionState.Open) await ((System.Data.Common.DbConnection)connection).OpenAsync();
            const string sql = "SELECT ProcedureId FROM DocumentProcedures WHERE DocumentId = @DocumentId;";
            return await connection.QueryAsync<int>(sql, new { DocumentId = documentId });
        }

        public async Task<bool> AddProcessLinkAsync(int documentId, int processId)
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection.State != System.Data.ConnectionState.Open) await ((System.Data.Common.DbConnection)connection).OpenAsync();
            const string sql = "INSERT INTO DocumentProcesses (DocumentId, ProcessId) VALUES (@DocumentId, @ProcessId) ON CONFLICT DO NOTHING;";
            var affected = await connection.ExecuteAsync(sql, new { DocumentId = documentId, ProcessId = processId });
            return affected > 0;
        }

        public async Task<bool> RemoveProcessLinkAsync(int documentId, int processId)
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection.State != System.Data.ConnectionState.Open) await ((System.Data.Common.DbConnection)connection).OpenAsync();
            const string sql = "DELETE FROM DocumentProcesses WHERE DocumentId = @DocumentId AND ProcessId = @ProcessId;";
            var affected = await connection.ExecuteAsync(sql, new { DocumentId = documentId, ProcessId = processId });
            return affected > 0;
        }

        private static string BuildWhereClause(
            DynamicParameters parameters,
            string? search,
            string? type,
            string? status,
            int? processId,
            int? procedureId,
            int? ownerUserId,
            int? organizationId,
            bool pendingValidationOnly,
            bool hidePendingValidationFromGlobal,
            int? restrictedUserId = null)
        {
            var conditions = new List<string>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                conditions.Add(@"(
                    d.Code ILIKE @Search
                    OR d.Title ILIKE @Search
                    OR COALESCE(d.Description, '') ILIKE @Search
                    OR COALESCE(d.Keywords, '') ILIKE @Search
                    OR COALESCE(p.Code, '') ILIKE @Search
                    OR COALESCE(p.Name, '') ILIKE @Search
                    OR COALESCE(pr.Code, '') ILIKE @Search
                )");
                parameters.Add("@Search", $"%{search.Trim()}%");
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                conditions.Add("d.Type = @Type");
                parameters.Add("@Type", type.Trim());
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (string.Equals(status.Trim(), "__APPROVED_OR_PUBLISHED__", StringComparison.OrdinalIgnoreCase))
                {
                    if (restrictedUserId.HasValue)
                    {
                        conditions.Add(@" (
                            COALESCE(cv.Status, 'BROUILLON') IN ('APPROUVE', 'PUBLIE')
                            OR p.PilotUserId = @RestrictedUserId
                            OR d.Id IN (
                                SELECT dp.DocumentId 
                                FROM DocumentProcesses dp 
                                INNER JOIN Processes pr ON pr.Id = dp.ProcessId 
                                WHERE pr.PilotUserId = @RestrictedUserId
                            )
                        ) ");
                    }
                    else
                    {
                        conditions.Add("COALESCE(cv.Status, 'BROUILLON') IN ('APPROUVE', 'PUBLIE')");
                    }
                }
                else
                {
                    conditions.Add("COALESCE(cv.Status, 'BROUILLON') = @Status");
                    parameters.Add("@Status", status.Trim());
                }
            }

            if (pendingValidationOnly)
            {
                conditions.Add("COALESCE(cv.Status, 'BROUILLON') IN ('BROUILLON', 'EN_REVISION')");
                conditions.Add("cv.ExpiryDate IS NOT NULL AND cv.ExpiryDate < NOW()");
            }
            else if (hidePendingValidationFromGlobal)
            {
                conditions.Add("NOT (COALESCE(cv.Status, 'BROUILLON') IN ('BROUILLON', 'EN_REVISION') AND cv.ExpiryDate IS NOT NULL AND cv.ExpiryDate < NOW())");
            }

            if (processId.HasValue)
            {
                conditions.Add(@" (
                    d.ProcessId = @ProcessId 
                    OR d.Id IN (SELECT DocumentId FROM DocumentProcesses WHERE ProcessId = @ProcessId)
                    OR d.ProcedureId IN (SELECT Id FROM Procedures WHERE ProcessId = @ProcessId)
                    OR d.Id IN (SELECT dp.DocumentId FROM DocumentProcedures dp INNER JOIN Procedures pr ON dp.ProcedureId = pr.Id WHERE pr.ProcessId = @ProcessId)
                ) ");
                parameters.Add("@ProcessId", processId.Value);
            }

            if (procedureId.HasValue)
            {
                conditions.Add("(d.ProcedureId = @ProcedureId OR d.Id IN (SELECT DocumentId FROM DocumentProcedures WHERE ProcedureId = @ProcedureId))");
                parameters.Add("@ProcedureId", procedureId.Value);
            }

            if (ownerUserId.HasValue)
            {
                conditions.Add("d.OwnerUserId = @OwnerUserId");
                parameters.Add("@OwnerUserId", ownerUserId.Value);
            }

            if (organizationId.HasValue)
            {
                conditions.Add("d.OrganizationId = @OrganizationId");
                parameters.Add("@OrganizationId", organizationId.Value);
            }

            if (restrictedUserId.HasValue)
            {
                conditions.Add(@" (
                    d.ProcessId IS NULL 
                    OR p.PilotUserId = @RestrictedUserId 
                    OR d.ProcessId IN (SELECT ProcessId FROM ProcessActors WHERE UserId = @RestrictedUserId)
                    OR d.OwnerUserId = @RestrictedUserId
                    OR d.Id IN (
                        SELECT dp.DocumentId 
                        FROM DocumentProcesses dp 
                        INNER JOIN Processes pr ON pr.Id = dp.ProcessId 
                        WHERE pr.PilotUserId = @RestrictedUserId 
                           OR pr.Id IN (SELECT ProcessId FROM ProcessActors WHERE UserId = @RestrictedUserId)
                    )
                    OR d.ProcedureId IN (
                        SELECT pr.Id 
                        FROM Procedures pr 
                        INNER JOIN Processes proc ON proc.Id = pr.ProcessId 
                        WHERE proc.PilotUserId = @RestrictedUserId 
                           OR proc.Id IN (SELECT ProcessId FROM ProcessActors WHERE UserId = @RestrictedUserId)
                    )
                    OR d.Id IN (
                        SELECT dpr.DocumentId 
                        FROM DocumentProcedures dpr 
                        INNER JOIN Procedures pr ON pr.Id = dpr.ProcedureId 
                        INNER JOIN Processes proc ON proc.Id = pr.ProcessId 
                        WHERE proc.PilotUserId = @RestrictedUserId 
                           OR proc.Id IN (SELECT ProcessId FROM ProcessActors WHERE UserId = @RestrictedUserId)
                    )
                ) ");
                parameters.Add("@RestrictedUserId", restrictedUserId.Value);
            }

            conditions.Add("d.IsActive = TRUE");
            conditions.Add("d.DeletedAt IS NULL");

            if (!conditions.Any())
            {
                return string.Empty;
            }

            return $"WHERE {string.Join(" AND ", conditions)}";
        }
    }
}
