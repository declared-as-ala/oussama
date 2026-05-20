namespace DocApi.DTOs.Notifications
{
    public class UpdateAlertRuleRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string TriggerType { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public decimal? ThresholdValue { get; set; }
    }
}
