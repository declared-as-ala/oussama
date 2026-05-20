using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.DTOs;

namespace DocApi.Services.Interfaces
{
    // Service interface for TypeDocument operations
    public interface ITypeDocumentService
    {
        Task<IEnumerable<TypeDocumentResponse>> GetAllAsync();
        Task<TypeDocumentResponse> GetByIdAsync(int id);
        Task<int> CreateAsync(CreateTypeDocumentRequest request);
        Task<bool> UpdateAsync(int id, UpdateTypeDocumentRequest request);
        Task<bool> DeleteAsync(int id);
    }
}
