namespace AudioGuide.Api.Models
{
    // Bản dịch chuỗi giao diện app (nút bấm, tab...) theo ngôn ngữ, dịch máy 1 lần rồi lưu lại
    public class UiTranslation
    {
        public string Lang { get; set; } = string.Empty;   // khóa chính, đồng thời khóa ngoại tới Languages.Code
        public string StringsJson { get; set; } = "{}";    // {key: text}
        public string SourceHash { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }

        public Language? Language { get; set; }
    }

    // Cache câu trả lời chatbot (khởi động lại server vẫn còn). Bản ghi quá hạn được dọn mỗi lần ghi.
    public class ChatCacheEntry
    {
        public string Key { get; set; } = string.Empty;    // SHA-256 của (ngôn ngữ + câu hỏi đã chuẩn hóa)
        public string AnswerJson { get; set; } = "{}";
        public DateTime ExpiresAt { get; set; }
    }

    // File audio thuyết minh lưu ngay trong SQL Server (cột VARBINARY(MAX))
    public class MediaFile
    {
        public string Key { get; set; } = string.Empty;    // vd "audio/<md5>.mp3"
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "audio/mpeg";
        public int Size { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
