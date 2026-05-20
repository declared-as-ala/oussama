using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class DocumentActionLogRepository : IDocumentActionLogRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public DocumentActionLogRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<int> CreateAsync(DocumentActionLog actionLog)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO DocumentActionLogs
                    (OrganizationId, DocumentId, DocumentVersionId, ActionType, OldValue, NewValue, Comment, PerformedByUserId, PerformedAt)
                VALUES
                    (@OrganizationId, @DocumentId, @DocumentVersionId, @ActionType, @OldValue, @NewValue, @Comment, @PerformedByUserId, @PerformedAt)
                RETURNING Id;";

            return await connection.QuerySingleAsync<int>(sql, actionLog);
        }

        public async Task<IEnumerable<DocumentActionLogData>> GetByDocumentIdAsync(int documentId, int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    l.Id,
                    l.OrganizationId,
                    l.DocumentId,
                    l.DocumentVersionId,
                    dv.VersionNumber,
                    l.ActionType,
                    l.OldValue,
                    l.NewValue,
                    l.Comment,
                    l.PerformedByUserId,
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), '') AS PerformedByFullName,
                    l.PerformedAt
                FROM DocumentActionLogs l
                LEFT JOIN DocumentVersions dv ON dv.Id = l.DocumentVersionId
                LEFT JOIN Users u ON u.Id = l.PerformedByUserId
                WHERE l.DocumentId = @DocumentId
                  AND l.OrganizationId = @OrganizationId
                ORDER BY l.PerformedAt DESC, l.Id DESC;";

            return await connection.QueryAsync<DocumentActionLogData>(sql, new
            {
                DocumentId = documentId,
                OrganizationId = organizationId
            });
        }
    }
}
