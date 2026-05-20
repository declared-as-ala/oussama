using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.DTOs.Notifications;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DocApi.Services
{
    public class DeviceTokenService : IDeviceTokenService
    {
        private readonly IUserDeviceRepository _userDeviceRepository;
        private readonly ILogger<DeviceTokenService> _logger;

        public DeviceTokenService(
            IUserDeviceRepository userDeviceRepository,
            ILogger<DeviceTokenService> logger)
        {
            _userDeviceRepository = userDeviceRepository;
            _logger = logger;
        }

        public async Task<int> RegisterAsync(UserContext userContext, RegisterDeviceTokenRequest request)
        {
            if (userContext.UserId <= 0)
            {
                throw new UnauthorizedException("Utilisateur non authentifie.");
            }

            var device = new UserDevice
            {
                UserId = userContext.UserId,
                DeviceToken = request.DeviceToken.Trim(),
                Platform = string.IsNullOrWhiteSpace(request.Platform) ? "unknown" : request.Platform.Trim().ToLowerInvariant(),
                DeviceName = string.IsNullOrWhiteSpace(request.DeviceName) ? null : request.DeviceName.Trim(),
                IsActive = true
            };

            var id = await _userDeviceRepository.UpsertAsync(device);
            _logger.LogInformation(
                "Device token registered. UserId={UserId}, Platform={Platform}, TokenPrefix={TokenPrefix}, RowId={RowId}",
                userContext.UserId,
                device.Platform,
                device.DeviceToken.Length > 12 ? device.DeviceToken[..12] : device.DeviceToken,
                id);

            return id;
        }

        public async Task<bool> UnregisterAsync(UserContext userContext, UnregisterDeviceTokenRequest request)
        {
            if (userContext.UserId <= 0)
            {
                throw new UnauthorizedException("Utilisateur non authentifie.");
            }

            var removed = await _userDeviceRepository.DeactivateAsync(userContext.UserId, request.DeviceToken.Trim());
            _logger.LogInformation(
                "Device token unregistered. UserId={UserId}, Removed={Removed}",
                userContext.UserId,
                removed);

            return removed;
        }
    }
}
