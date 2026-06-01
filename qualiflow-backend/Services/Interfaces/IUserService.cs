using System.Threading.Tasks;
using DocApi.DTOs.Auth;
using DocApi.DTOs.Users;

namespace DocApi.Services.Interfaces
{
    public interface IUserService
    {
        Task<UserResponse> GetByIdAsync(int id);
        Task<UserListResponse> GetAllAsync(int organizationId, int page = 1, int pageSize = 10);
        Task<UserListResponse> SearchAsync(string? searchTerm, int? organizationId, int page = 1, int pageSize = 10);
        Task<int> CreateAsync(CreateUserRequest request, int? requestingUserId = null);
        Task<bool> UpdateAsync(int id, UpdateUserRequest request, int? requestingUserId = null);
        Task<bool> ToggleStatusAsync(int id, bool isActive, int? requestingUserId = null);
        Task<bool> ChangeRoleAsync(int id, ChangeUserRoleRequest request, int? requestingUserId = null);
        Task<bool> DeleteAsync(int id, int? requestingUserId = null);
        Task<bool> HardDeleteAsync(int id);
    }
}
