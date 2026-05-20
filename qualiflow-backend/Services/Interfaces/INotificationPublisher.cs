using System.Threading;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Services.Interfaces
{
    public interface INotificationPublisher
    {
        Task PublishAsync(NotificationEventMessage message, CancellationToken cancellationToken = default);
    }
}
