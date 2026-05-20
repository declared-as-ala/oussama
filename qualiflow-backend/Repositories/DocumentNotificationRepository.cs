using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DocApi.Domain.Entities;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;

namespace DocApi.Repositories
{
    public class DocumentNotificationRepository : IDocumentNotificationRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public DocumentNotificationRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<int> CreateAsync(DocumentNotification notification)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO document_notifications
                    (organization_id, document_id, document_version_id, event_type, recipient_user_id, recipient_role, channel, subject, message, delivery_status, external_message_id, payload_json, sent_at, created_at)
                VALUES
                    (@OrganizationId, @DocumentId, @DocumentVersionId, @EventType, @RecipientUserId, @RecipientRole, @Channel, @Subject, @Message, @DeliveryStatus, @ExternalMessageId, @PayloadJson, @SentAt, @CreatedAt)
                RETURNING id;";

            return await connection.QuerySingleAsync<int>(sql, notification);
        }

        public async Task<int> CreateBatchAsync(IEnumerable<DocumentNotification> notifications)
        {
            var list = notifications.ToList();
            if (list.Count == 0)
            {
                return 0;
            }

            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO document_notifications
                    (organization_id, document_id, document_version_id, event_type, recipient_user_id, recipient_role, channel, subject, message, delivery_status, external_message_id, payload_json, sent_at, created_at)
                VALUES
                    (@OrganizationId, @DocumentId, @DocumentVersionId, @EventType, @RecipientUserId, @RecipientRole, @Channel, @Subject, @Message, @DeliveryStatus, @ExternalMessageId, @PayloadJson, @SentAt, @CreatedAt);";

            return await connection.ExecuteAsync(sql, list);
        }
    }
}
