using System.Collections.Generic;

namespace DocApi.DTOs.Procedures
{
    public class ProcedureStatisticsResponse
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public int Inactive { get; set; }
        public int WithResponsible { get; set; }
        public int WithoutResponsible { get; set; }
        public Dictionary<string, int> ByStatus { get; set; } = new();
    }
}
