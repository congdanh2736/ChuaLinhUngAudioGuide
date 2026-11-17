namespace AudioGuide.Api.Models
{
    // Bài viết kiến thức (FAQ, quy định...) làm tài liệu cho chatbot
    public class KnowledgeArticle
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // Lịch sử hỏi - đáp của chatbot
    public class ChatLog
    {
        public int Id { get; set; }
        public int? SessionId { get; set; }                 // null = admin xem thử / không cần mã
        public string Lang { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public List<string> SourceIds { get; set; } = new(); // vd "poi:3", "article:2" (chỉ để tham khảo)
        public bool UsedLlm { get; set; }
        public DateTime CreatedAt { get; set; }

        public AccessSession? Session { get; set; }
        public Language? Language { get; set; }
    }

    // Lượt xem / nghe / đi qua 1 điểm tham quan (dùng cho thống kê và "khung giờ đông khách")
    public class VisitEvent
    {
        public int Id { get; set; }
        public int? SessionId { get; set; }
        public int PoiId { get; set; }
        public string Lang { get; set; } = string.Empty;
        public EventType Type { get; set; }
        public DateTime CreatedAt { get; set; }

        public AccessSession? Session { get; set; }
        public Poi? Poi { get; set; }
    }
}
