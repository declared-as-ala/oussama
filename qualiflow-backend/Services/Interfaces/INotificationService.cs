using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Notifications;

namespace DocApi.Services.Interfaces
{
    public interface INotificationService
    {
        Task<PagedNotificationResponse> GetNotificationsAsync(GetNotificationsQueryRequest query, UserContext userContext);
        Task<NotificationResponse> GetByIdAsync(int id, UserContext userContext);
        Task<int> GetUnreadCountAsync(UserContext userContext);
        Task<NotificationStatisticsResponse> GetStatisticsAsync(UserContext userContext);
        Task<NotificationResponse> MarkAsReadAsync(int id, UserContext userContext);
        Task<int> MarkAllAsReadAsync(UserContext userContext);
        Task<NotificationResponse> ArchiveAsync(int id, UserContext userContext);
        Task<bool> DeleteAsync(int id, UserContext userContext);
        Task<IReadOnlyList<NotificationResponse>> CreateAsync(CreateNotificationRequest request, UserContext userContext);
        Task<IReadOnlyList<NotificationRecipientResponse>> GetRecipientsAsync(NotificationRecipientsQueryRequest query, UserContext userContext);
        Task<NotificationLogResponse> LogDocumentNotificationAsync(NotificationLogRequest request, int organizationId, int? triggeredByUserId);
    }
}
