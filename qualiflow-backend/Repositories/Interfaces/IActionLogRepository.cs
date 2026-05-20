using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IActionLogRepository
    {
        Task<int> CreateAsync(ActionLog log);
        Task<List<ActionLog>> GetByOrganizationIdAsync(int organizationId, int limit = 50);
    }
}
