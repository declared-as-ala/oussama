using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface INotificationRuleRepository
    {
        Task<IReadOnlyList<NotificationRule>> GetByEventTypeAsync(int organizationId, string eventType);
    }
}
