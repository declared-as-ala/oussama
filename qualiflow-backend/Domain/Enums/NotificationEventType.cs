namespace DocApi.Domain.Enums
{
    public enum NotificationEventType
    {
        DocumentCreated = 0,
        DocumentSubmitted = 1,
        DocumentApproved = 2,
        DocumentRejected = 3,
        DocumentArchived = 4,
        DocumentExpiring30 = 5,
        DocumentExpiring7 = 6,
        DocumentExpiring1 = 7,
        DocumentExpired = 8
    }
}
