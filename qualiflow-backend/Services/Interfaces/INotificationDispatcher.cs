using System.Threading;
using System.Threading.Tasks;

namespace DocApi.Services.Interfaces
{
    public interface INotificationDispatcher
    {
        Task<int> DispatchPendingNotificationsAsync(CancellationToken cancellationToken = default);
    }
}
