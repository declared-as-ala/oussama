using System.Threading;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Services.Interfaces
{
    public interface INotificationConsumerService
    {
        Task HandleAsync(NotificationEventMessage message, CancellationToken cancellationToken = default);
    }
}
