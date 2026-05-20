using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IProcessActorRepository
    {
        Task<IEnumerable<ProcessActorDetails>> GetActorsByProcessIdAsync(int processId);
        Task ReplaceActorsAsync(int processId, int organizationId, IEnumerable<ProcessActor> actors);
        Task<bool> RemoveActorAsync(int processId, int userId);
        Task<bool> HasActorAsync(int processId, int userId);
    }
}
