using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Domain.Enums;
using DocApi.DTOs.Notifications;

namespace DocApi.Services.Interfaces
{
    public interface INotificationRecipientService
    {
        Task<IReadOnlyList<NotificationRecipientResponse>> GetRecipientsAsync(
            int organizationId,
            NotificationEventType eventType,
            int? documentId);
    }
}
