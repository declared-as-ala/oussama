namespace DocApi.DTOs.Notifications
{
    public class NotificationRecipientResponse
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string RoleType { get; set; } = string.Empty;
    }
}
