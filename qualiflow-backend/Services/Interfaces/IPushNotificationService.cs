using System.Threading;
using System.Threading.Tasks;
using DocApi.Domain.Entities;
using DocApi.Services.Models;

namespace DocApi.Services.Interfaces
{
    public interface IPushNotificationService
    {
        Task<PushDispatchResult> SendAsync(Notification notification, CancellationToken cancellationToken = default);
    }
}
