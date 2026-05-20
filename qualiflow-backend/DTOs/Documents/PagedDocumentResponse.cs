using System.Collections.Generic;

namespace DocApi.DTOs.Documents
{
    public class PagedDocumentResponse
    {
        public int Total { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public List<DocumentListItemResponse> Items { get; set; } = new();
    }
}

