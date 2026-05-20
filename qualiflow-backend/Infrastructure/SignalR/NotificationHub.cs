using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DocApi.Infrastructure.SignalR
{
    [Authorize]
    public sealed class NotificationHub : Hub
    {
    }
}
