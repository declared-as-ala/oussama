using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface INotificationRepository
    {
        Task<IEnumerable<Notification>> SearchAsync(
            int pageNumber,
            int pageSize,
            int userId,
            int? organizationId,
            string? search,
            bool? isRead,
            string? category,
            string? priority,
            string? type,
            DateTime? fromDate,
            DateTime? toDate,
            bool includeArchived = false);

        Task<int> CountSearchAsync(
            int userId,
            int? organizationId,
            string? search,
            bool? isRead,
            string? category,
            string? priority,
            string? type,
            DateTime? fromDate,
            DateTime? toDate,
            bool includeArchived = false);

        Task<IEnumerable<Notification>> GetForUserAsync(
            int userId,
            int? organizationId,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool includeArchived = true);

        Task<Notification?> GetByIdAsync(int id);
        Task<int> CreateAsync(Notification notification);
        Task<int> CreateBatchAsync(IEnumerable<Notification> notifications);
        Task<bool> MarkAsReadAsync(int id, int userId, DateTime readAtUtc);
        Task<int> MarkAllAsReadAsync(int userId, int? organizationId, DateTime readAtUtc);
        Task<bool> ArchiveAsync(int id, int userId);
        Task<bool> DeleteAsync(int id, int userId);
        Task<int> GetUnreadCountAsync(int userId, int? organizationId);
        Task<bool> MarkPushSentAsync(int id, string? externalProviderId, string channel);
        Task<bool> ExistsSimilarInWindowAsync(
            int userId,
            string type,
            string? referenceType,
            string? referenceId,
            DateTime fromUtc);
    }
}
