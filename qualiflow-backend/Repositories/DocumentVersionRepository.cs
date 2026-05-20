using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class DocumentVersionRepository : IDocumentVersionRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public DocumentVersionRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<DocumentVersion?> GetByIdAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM DocumentVersions WHERE Id = @Id";
            return await connection.QueryFirstOrDefaultAsync<DocumentVersion>(sql, new { Id = id });
        }

        public async Task<DocumentVersion?> GetByDocumentAndVersionIdAsync(int documentId, int versionId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM DocumentVersions WHERE Id = @VersionId AND DocumentId = @DocumentId";
            return await connection.QueryFirstOrDefaultAsync<DocumentVersion>(sql, new { VersionId = versionId, DocumentId = documentId });
        }

        public async Task<DocumentVersionData?> GetDetailsByIdAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    dv.Id,
                    dv.DocumentId,
                    dv.OrganizationId,
                    dv.VersionNumber,
                    dv.Status,
                    dv.FileName,
                    dv.OriginalFileName,
                    dv.FilePath,
                    dv.MimeType,
                    dv.FileSize,
                    dv.RevisionComment,
                    dv.Signature,
                    dv.EffectiveDate,
                    dv.ExpiryDate,
                    dv.IsCurrent,
                    dv.EstablishedByUserId,
                    NULLIF(TRIM(COALESCE(eb.FirstName, '') || ' ' || COALESCE(eb.LastName, '')), '') AS EstablishedByUserFullName,
                    dv.EstablishedAt,
                    dv.VerifiedByUserId,
                    NULLIF(TRIM(COALESCE(vb.FirstName, '') || ' ' || COALESCE(vb.LastName, '')), '') AS VerifiedByUserFullName,
                    dv.VerifiedAt,
                    dv.ValidatedByUserId,
                    NULLIF(TRIM(COALESCE(vab.FirstName, '') || ' ' || COALESCE(vab.LastName, '')), '') AS ValidatedByUserFullName,
                    dv.ValidatedAt,
                    dv.CreatedAt,
                    dv.UpdatedAt
                FROM DocumentVersions dv
                LEFT JOIN Users eb ON eb.Id = dv.EstablishedByUserId
                LEFT JOIN Users vb ON vb.Id = dv.VerifiedByUserId
                LEFT JOIN Users vab ON vab.Id = dv.ValidatedByUserId
                WHERE dv.Id = @Id;";

            return await connection.QueryFirstOrDefaultAsync<DocumentVersionData>(sql, new { Id = id });
        }

        public async Task<DocumentVersionData?> GetCurrentByDocumentIdAsync(int documentId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    dv.Id,
                    dv.DocumentId,
                    dv.OrganizationId,
                    dv.VersionNumber,
                    dv.Status,
                    dv.FileName,
                    dv.OriginalFileName,
                    dv.FilePath,
                    dv.MimeType,
                    dv.FileSize,
                    dv.RevisionComment,
                    dv.Signature,
                    dv.EffectiveDate,
                    dv.ExpiryDate,
                    dv.IsCurrent,
                    dv.EstablishedByUserId,
                    NULLIF(TRIM(COALESCE(eb.FirstName, '') || ' ' || COALESCE(eb.LastName, '')), '') AS EstablishedByUserFullName,
                    dv.EstablishedAt,
                    dv.VerifiedByUserId,
                    NULLIF(TRIM(COALESCE(vb.FirstName, '') || ' ' || COALESCE(vb.LastName, '')), '') AS VerifiedByUserFullName,
                    dv.VerifiedAt,
                    dv.ValidatedByUserId,
                    NULLIF(TRIM(COALESCE(vab.FirstName, '') || ' ' || COALESCE(vab.LastName, '')), '') AS ValidatedByUserFullName,
                    dv.ValidatedAt,
                    dv.CreatedAt,
                    dv.UpdatedAt
                FROM DocumentVersions dv
                LEFT JOIN Users eb ON eb.Id = dv.EstablishedByUserId
                LEFT JOIN Users vb ON vb.Id = dv.VerifiedByUserId
                LEFT JOIN Users vab ON vab.Id = dv.ValidatedByUserId
                WHERE dv.DocumentId = @DocumentId
                  AND dv.IsCurrent = TRUE
                ORDER BY dv.Id DESC
                LIMIT 1;";

            return await connection.QueryFirstOrDefaultAsync<DocumentVersionData>(sql, new { DocumentId = documentId });
        }

        public async Task<IEnumerable<DocumentVersionData>> GetByDocumentIdAsync(int documentId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    dv.Id,
                    dv.DocumentId,
                    dv.OrganizationId,
                    dv.VersionNumber,
                    dv.Status,
                    dv.FileName,
                    dv.OriginalFileName,
                    dv.FilePath,
                    dv.MimeType,
                    dv.FileSize,
                    dv.RevisionComment,
                    dv.Signature,
                    dv.EffectiveDate,
                    dv.ExpiryDate,
                    dv.IsCurrent,
                    dv.EstablishedByUserId,
                    NULLIF(TRIM(COALESCE(eb.FirstName, '') || ' ' || COALESCE(eb.LastName, '')), '') AS EstablishedByUserFullName,
                    dv.EstablishedAt,
                    dv.VerifiedByUserId,
                    NULLIF(TRIM(COALESCE(vb.FirstName, '') || ' ' || COALESCE(vb.LastName, '')), '') AS VerifiedByUserFullName,
                    dv.VerifiedAt,
                    dv.ValidatedByUserId,
                    NULLIF(TRIM(COALESCE(vab.FirstName, '') || ' ' || COALESCE(vab.LastName, '')), '') AS ValidatedByUserFullName,
                    dv.ValidatedAt,
                    dv.CreatedAt,
                    dv.UpdatedAt
                FROM DocumentVersions dv
                LEFT JOIN Users eb ON eb.Id = dv.EstablishedByUserId
                LEFT JOIN Users vb ON vb.Id = dv.VerifiedByUserId
                LEFT JOIN Users vab ON vab.Id = dv.ValidatedByUserId
                WHERE dv.DocumentId = @DocumentId
                ORDER BY dv.CreatedAt DESC, dv.Id DESC;";

            return await connection.QueryAsync<DocumentVersionData>(sql, new { DocumentId = documentId });
        }

        public async Task<byte[]?> GetFileContentAsync(int versionId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT FileContent FROM DocumentVersions WHERE Id = @VersionId";
            return await connection.QueryFirstOrDefaultAsync<byte[]>(sql, new { VersionId = versionId });
        }

        public async Task<bool> ExistsVersionNumberAsync(int documentId, string versionNumber, int? excludeId = null)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT COUNT(1)
                FROM DocumentVersions
                WHERE DocumentId = @DocumentId
                  AND LOWER(VersionNumber) = LOWER(@VersionNumber)
                  AND (@ExcludeId IS NULL OR Id <> @ExcludeId);";

            var count = await connection.QuerySingleAsync<int>(sql, new
            {
                DocumentId = documentId,
                VersionNumber = versionNumber,
                ExcludeId = excludeId
            });

            return count > 0;
        }

        public async Task<int> CreateAsync(DocumentVersion version)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO DocumentVersions
                    (DocumentId, OrganizationId, VersionNumber, Status, FileName, OriginalFileName, FilePath, FileExtension, MimeType, FileSize, FileContent, RevisionComment, Signature, EstablishedByUserId, EstablishedAt, VerifiedByUserId, VerifiedAt, ValidatedByUserId, ValidatedAt, EffectiveDate, ExpiryDate, IsCurrent, CreatedAt, UpdatedAt)
                VALUES
                    (@DocumentId, @OrganizationId, @VersionNumber, @Status, @FileName, @OriginalFileName, @FilePath, @FileExtension, @MimeType, @FileSize, @FileContent, @RevisionComment, @Signature, @EstablishedByUserId, @EstablishedAt, @VerifiedByUserId, @VerifiedAt, @ValidatedByUserId, @ValidatedAt, @EffectiveDate, @ExpiryDate, @IsCurrent, @CreatedAt, @UpdatedAt)
                RETURNING Id;";

            return await connection.QuerySingleAsync<int>(sql, version);
        }

        public async Task<bool> UpdateAsync(DocumentVersion version)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                UPDATE DocumentVersions
                SET VersionNumber = @VersionNumber,
                    Status = @Status,
                    FileName = @FileName,
                    OriginalFileName = @OriginalFileName,
                    FilePath = @FilePath,
                    FileExtension = @FileExtension,
                    MimeType = @MimeType,
                    FileSize = @FileSize,
                    FileContent = @FileContent,
                    RevisionComment = @RevisionComment,
                    Signature = @Signature,
                    EstablishedByUserId = @EstablishedByUserId,
                    EstablishedAt = @EstablishedAt,
                    VerifiedByUserId = @VerifiedByUserId,
                    VerifiedAt = @VerifiedAt,
                    ValidatedByUserId = @ValidatedByUserId,
                    ValidatedAt = @ValidatedAt,
                    EffectiveDate = @EffectiveDate,
                    ExpiryDate = @ExpiryDate,
                    IsCurrent = @IsCurrent,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id;";

            var rows = await connection.ExecuteAsync(sql, version);
            return rows > 0;
        }

        public async Task<bool> UpdateStatusAsync(
            int versionId,
            string status,
            string? revisionComment,
            int? verifiedByUserId,
            System.DateTime? verifiedAt,
            int? validatedByUserId,
            System.DateTime? validatedAt,
            System.DateTime? updatedAt)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                UPDATE DocumentVersions
                SET Status = @Status,
                    RevisionComment = @RevisionComment,
                    VerifiedByUserId = COALESCE(@VerifiedByUserId, VerifiedByUserId),
                    VerifiedAt = COALESCE(@VerifiedAt, VerifiedAt),
                    ValidatedByUserId = COALESCE(@ValidatedByUserId, ValidatedByUserId),
                    ValidatedAt = COALESCE(@ValidatedAt, ValidatedAt),
                    UpdatedAt = @UpdatedAt
                WHERE Id = @VersionId;";

            var rows = await connection.ExecuteAsync(sql, new
            {
                VersionId = versionId,
                Status = status,
                RevisionComment = revisionComment,
                VerifiedByUserId = verifiedByUserId,
                VerifiedAt = verifiedAt,
                ValidatedByUserId = validatedByUserId,
                ValidatedAt = validatedAt,
                UpdatedAt = updatedAt
            });

            return rows > 0;
        }

        public async Task<bool> SetCurrentVersionAsync(int documentId, int versionId)
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection.State != ConnectionState.Open) connection.Open();
            using var transaction = connection.BeginTransaction();

            const string resetSql = @"
                UPDATE DocumentVersions
                SET IsCurrent = FALSE,
                    UpdatedAt = NOW()
                WHERE DocumentId = @DocumentId;";

            const string setSql = @"
                UPDATE DocumentVersions
                SET IsCurrent = TRUE,
                    UpdatedAt = NOW()
                WHERE Id = @VersionId
                  AND DocumentId = @DocumentId;";

            await connection.ExecuteAsync(resetSql, new { DocumentId = documentId }, transaction);
            var rows = await connection.ExecuteAsync(setSql, new { VersionId = versionId, DocumentId = documentId }, transaction);

            transaction.Commit();
            return rows > 0;
        }
    }
}
