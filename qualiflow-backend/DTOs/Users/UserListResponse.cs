namespace DocApi.DTOs.Users
{
    public class UserListResponse
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<UserResponse> Items { get; set; } = new();
    }
}
