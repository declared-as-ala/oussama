namespace DocApi.DTOs.Procedures
{
    public class UpdateInstructionRequest
    {
        public int? Id { get; set; }
        public required string Code { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = "ACTIF";
        public int? OrderIndex { get; set; }
    }
}
