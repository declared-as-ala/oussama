using System.Collections.Generic;

namespace DocApi.DTOs.Indicators
{
    public class PagedIndicatorResponse
    {
        public int Total { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public List<IndicatorListItemResponse> Items { get; set; } = new();
    }
}
