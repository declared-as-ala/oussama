namespace DocApi.Infrastructure
{
    public class StoredFileInfo
    {
        public required string FileName { get; set; }
        public required string OriginalFileName { get; set; }
        public required string RelativePath { get; set; }
        public required string AbsolutePath { get; set; }
        public required string FileExtension { get; set; }
        public required string MimeType { get; set; }
        public long FileSize { get; set; }
    }
}
