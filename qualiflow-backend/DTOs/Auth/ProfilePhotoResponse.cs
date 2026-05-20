namespace DocApi.DTOs.Auth
{
    public class ProfilePhotoResponse
    {
        public int UserId { get; set; }
        public string? ProfilePhotoPath { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
