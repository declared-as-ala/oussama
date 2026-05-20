using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface INotificationPreferenceRepository
    {
        Task<IEnumerable<NotificationPreference>> GetByUserIdAsync(int userId);
        Task<NotificationPreference?> GetByUserAndTypeAsync(int userId, string notificationType);
        Task<int> UpsertAsync(NotificationPreference preference);
    }
}
