using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IPasswordResetTokenRepository
    {
        Task<PasswordResetToken?> GetByTokenAsync(string token);
        Task<PasswordResetToken?> GetByUserAndTokenAsync(int userId, string token);
        Task<int> CreateAsync(PasswordResetToken token);
        Task<bool> MarkAsUsedAsync(int id);
        Task<bool> RevokeActiveByUserIdAsync(int userId);
        Task<bool> DeleteExpiredAsync();
    }
}
