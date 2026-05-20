using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class DocumentExpirationPolicyRepository : IDocumentExpirationPolicyRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public DocumentExpirationPolicyRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<DocumentExpirationPolicy?> GetByOrganizationIdAsync(int organizationId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT
                    id AS Id,
                    organization_id AS OrganizationId,
                    alert_days_30 AS AlertDays30,
                    alert_days_7 AS AlertDays7,
                    alert_days_1 AS AlertDays1,
                    is_active AS IsActive,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt
                FROM document_expiration_policies
                WHERE organization_id = @OrganizationId
                ORDER BY id DESC
                LIMIT 1;";

            return await connection.QueryFirstOrDefaultAsync<DocumentExpirationPolicy>(sql, new { OrganizationId = organizationId });
        }
    }
}
