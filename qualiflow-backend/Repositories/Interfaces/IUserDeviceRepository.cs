using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Entities;

namespace DocApi.Repositories.Interfaces
{
    public interface IUserDeviceRepository
    {
        Task<int> UpsertAsync(UserDevice device);
        Task<bool> DeactivateAsync(int userId, string deviceToken);
        Task<bool> DeactivateByTokenAsync(string deviceToken);
        Task<IEnumerable<UserDevice>> GetActiveByUserIdAsync(int userId);
    }
}
