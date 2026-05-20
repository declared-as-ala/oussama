using System.Collections.Generic;

namespace DocApi.DTOs.Procedures
{
    public class PagedProcedureResponse
    {
        public int Total { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public List<ProcedureListItemResponse> Items { get; set; } = new();
    }
}
