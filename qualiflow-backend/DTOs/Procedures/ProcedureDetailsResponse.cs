using System.Collections.Generic;

namespace DocApi.DTOs.Procedures
{
    public class ProcedureDetailsResponse
    {
        public required ProcedureResponse Procedure { get; set; }
        public List<InstructionResponse> Instructions { get; set; } = new();
    }
}
