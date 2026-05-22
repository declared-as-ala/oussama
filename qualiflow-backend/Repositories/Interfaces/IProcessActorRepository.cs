using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IProcessActorRepository
    {
        Task<IEnumerable<ProcessActorDetails>> GetActorsByProcessIdAsync(int processId);
        Task<bool> AddActorIfMissingAsync(int processId, int organizationId, int userId, string actorType);
        Task ReplaceActorsAsync(int processId, int organizationId, IEnumerable<ProcessActor> actors);
        Task<bool> RemoveActorAsync(int processId, int userId);
        Task<bool> HasActorAsync(int processId, int userId);
    }
}
