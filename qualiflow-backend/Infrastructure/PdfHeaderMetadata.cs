using System;

namespace DocApi.Infrastructure
{
    public sealed class PdfHeaderMetadata
    {
        public string OrganizationName { get; set; } = string.Empty;
        public string OrganizationCode { get; set; } = string.Empty;
        public string? OrganizationLogoPath { get; set; }
        public string? OrganizationEmail { get; set; }
        public string? OrganizationPhone { get; set; }
        public string ProcessCode { get; set; } = string.Empty;
        public string ProcedureCode { get; set; } = string.Empty;
        public string DocumentCode { get; set; } = string.Empty;
        public string DocumentTitle { get; set; } = string.Empty;
        public string VersionNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? SignatureBase64 { get; set; }
        public string? SignerRole { get; set; }
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
