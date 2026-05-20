using DocApi.DTOs.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocApi.Controllers
{
    [ApiController]
    [Route("api/device-tokens")]
    [Authorize]
    public class DeviceTokensController : ControllerBase
    {
        [HttpPost("register")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,CHEF_SERVICE,UTILISATEUR")]
        public ActionResult<object> Register([FromBody] RegisterDeviceTokenRequest request)
        {
            _ = request;
            return StatusCode(410, new
            {
                message = "Device token registration is deprecated. Use OneSignal SDK login(external_id) and tags instead.",
                success = false
            });
        }

        [HttpPost("unregister")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,CHEF_SERVICE,UTILISATEUR")]
        public ActionResult<object> Unregister([FromBody] UnregisterDeviceTokenRequest request)
        {
            _ = request;
            return StatusCode(410, new
            {
                message = "Device token unregister is deprecated. Use OneSignal SDK logout() on sign-out.",
                success = false
            });
        }
    }
}
