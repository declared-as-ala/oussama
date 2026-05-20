using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IDocumentNotificationRepository
    {
        Task<int> CreateAsync(DocumentNotification notification);
        Task<int> CreateBatchAsync(IEnumerable<DocumentNotification> notifications);
    }
}
