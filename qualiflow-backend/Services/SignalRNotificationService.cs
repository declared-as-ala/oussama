using System.Threading.Tasks;
using DocApi.Domain.Entities;
using DocApi.DTOs.Notifications;
using DocApi.Infrastructure.SignalR;
using Microsoft.AspNetCore.SignalR;

namespace DocApi.Services
{
    public sealed class SignalRNotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public SignalRNotificationService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task SendToUserAsync(Notification notification, int unreadCount)
        {
            var payload = MapToResponse(notification);
            var userKey = notification.UserId.ToString();

            await Task.WhenAll(
                _hubContext.Clients.User(userKey).SendAsync("notificationReceived", payload),
                _hubContext.Clients.User(userKey).SendAsync("unreadCountUpdated", unreadCount));
        }

        private static NotificationResponse MapToResponse(Notification notification)
        {
            return new NotificationResponse
            {
                Id = notification.Id,
                OrganizationId = notification.OrganizationId,
                UserId = notification.UserId,
                SenderId = notification.SenderId,
                Type = notification.Type,
                Category = notification.Category,
                Title = notification.Title,
                Message = notification.Message,
                Priority = notification.Priority,
                IsRead = notification.IsRead,
                ReadAt = notification.ReadAt,
                IsPushSent = notification.IsPushSent,
                IsArchived = notification.IsArchived,
                EntityType = notification.EntityType,
                EntityId = notification.EntityId,
                SourceModule = notification.SourceModule,
                RedirectUrl = notification.RedirectUrl,
                ExpiresAt = notification.ExpiresAt,
                ReferenceType = notification.ReferenceType,
                ReferenceId = notification.ReferenceId,
                ActionUrl = notification.ActionUrl,
                CreatedAt = notification.CreatedAt
            };
        }
    }
}
