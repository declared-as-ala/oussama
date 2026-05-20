using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IWebPushSubscriptionRepository
    {
        Task<int> UpsertAsync(WebPushSubscription subscription);
        Task<int> DeactivateAsync(int userId, string endpoint);
        Task<IReadOnlyList<WebPushSubscription>> GetActiveByUserAsync(int userId);
    }
}
