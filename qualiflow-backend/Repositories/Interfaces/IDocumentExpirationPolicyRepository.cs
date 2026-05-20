using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IDocumentExpirationPolicyRepository
    {
        Task<DocumentExpirationPolicy?> GetByOrganizationIdAsync(int organizationId);
    }
}
