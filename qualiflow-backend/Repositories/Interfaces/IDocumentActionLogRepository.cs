using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IDocumentActionLogRepository
    {
        Task<int> CreateAsync(DocumentActionLog actionLog);
        Task<IEnumerable<DocumentActionLogData>> GetByDocumentIdAsync(int documentId, int organizationId);
    }
}
