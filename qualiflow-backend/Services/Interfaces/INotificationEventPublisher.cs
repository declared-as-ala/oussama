using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DocApi.Services.Interfaces
{
    public interface INotificationEventPublisher
    {
        Task PublishToUserAsync(
            int organizationId,
            int userId,
            string type,
            string category,
            string title,
            string message,
            string priority,
            string? referenceType,
            string? referenceId,
            string? actionUrl,
            int? triggeredByUserId = null,
            CancellationToken cancellationToken = default);

        Task PublishToUsersAsync(
            int organizationId,
            IEnumerable<int> userIds,
            string type,
            string category,
            string title,
            string message,
            string priority,
            string? referenceType,
            string? referenceId,
            string? actionUrl,
            int? triggeredByUserId = null,
            CancellationToken cancellationToken = default);

        Task PublishToRolesAsync(
            int organizationId,
            IEnumerable<string> roles,
            string type,
            string category,
            string title,
            string message,
            string priority,
            string? referenceType,
            string? referenceId,
            string? actionUrl,
            int? triggeredByUserId = null,
            CancellationToken cancellationToken = default);
    }
}
