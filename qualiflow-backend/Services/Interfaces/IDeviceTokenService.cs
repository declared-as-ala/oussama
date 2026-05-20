using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Notifications;

namespace DocApi.Services.Interfaces
{
    public interface IDeviceTokenService
    {
        Task<int> RegisterAsync(UserContext userContext, RegisterDeviceTokenRequest request);
        Task<bool> UnregisterAsync(UserContext userContext, UnregisterDeviceTokenRequest request);
    }
}
