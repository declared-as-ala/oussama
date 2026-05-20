using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    // Interface for TypeDocument repository operations
    public interface ITypeDocumentRepository
    {
        Task<IEnumerable<TypeDocument>> GetAllAsync();
        Task<TypeDocument?> GetByIdAsync(int id);
        Task<int> CreateAsync(TypeDocument entity);
        Task<bool> UpdateAsync(TypeDocument entity);
        Task<bool> DeleteAsync(int id);
    }
}
