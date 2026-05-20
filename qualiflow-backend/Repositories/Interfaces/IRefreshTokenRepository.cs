using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken?> GetByTokenAsync(string token);
        Task<int> CreateAsync(RefreshToken refreshToken);
        Task<bool> RevokeAsync(int id);
        Task<bool> RevokeByUserIdAsync(int userId);
        Task<bool> DeleteExpiredAsync();
    }
}
