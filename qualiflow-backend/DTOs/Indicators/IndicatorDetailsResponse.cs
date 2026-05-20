using System.Collections.Generic;

namespace DocApi.DTOs.Indicators
{
    public class IndicatorDetailsResponse
    {
        public required IndicatorResponse Indicator { get; set; }
        public required IndicatorLinkedProcessResponse Process { get; set; }
        public required IndicatorResponsibleResponse Responsible { get; set; }
        public IndicatorValueResponse? LatestValue { get; set; }
        public bool IsInAlert { get; set; }
        public List<IndicatorValueResponse> ValuesHistory { get; set; } = new();
        public List<IndicatorAlertResponse> Alerts { get; set; } = new();
    }

    public class IndicatorLinkedProcessResponse
    {
        public int Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
    }

    public class IndicatorResponsibleResponse
    {
        public int Id { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
    }
}
