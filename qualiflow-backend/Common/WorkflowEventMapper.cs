using System;
using DocApi.Domain.Enums;

namespace DocApi.Common
{
    public static class WorkflowEventMapper
    {
        public static NotificationEventType ParseEventType(string value)
        {
            if (Enum.TryParse<NotificationEventType>(value, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            throw new ServiceException($"Type d'evenement de notification invalide: {value}.");
        }
    }
}
