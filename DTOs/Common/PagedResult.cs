namespace AudioGuide.Api.DTOs.Common
{
    // Kết quả phân trang: danh sách 1 trang + tổng số bản ghi
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int Size { get; set; }
    }

    public class MessageDto
    {
        public string Message { get; set; } = string.Empty;
        public MessageDto() { }
        public MessageDto(string message) => Message = message;
    }
}
