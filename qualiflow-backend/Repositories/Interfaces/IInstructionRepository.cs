using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IInstructionRepository
    {
        Task<IEnumerable<Instruction>> GetByProcedureIdAsync(int procedureId);
        Task<Instruction?> GetByIdAsync(int id);
        Task<bool> ExistsCodeAsync(int procedureId, string code, int? excludeId = null);
        Task<int> GetNextOrderIndexAsync(int procedureId);
        Task<int> CreateAsync(Instruction instruction);
        Task<bool> UpdateAsync(Instruction instruction);
        Task<bool> DeleteAsync(int id);
    }
}
