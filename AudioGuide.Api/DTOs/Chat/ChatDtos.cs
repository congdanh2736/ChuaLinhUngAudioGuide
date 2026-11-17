namespace AudioGuide.Api.DTOs.Chat
{
    public class ChatRequestDto
    {
        public string Question { get; set; } = string.Empty;
        public string Lang { get; set; } = "vi";
    }

    public class ChatSourceDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;   // poi | article
        public string Title { get; set; } = string.Empty;
    }

    public class ChatAnswerDto
    {
        public string Answer { get; set; } = string.Empty;
        public string Lang { get; set; } = string.Empty;
        public List<ChatSourceDto> Sources { get; set; } = new();
        public bool UsedLlm { get; set; }     // true = AI tạo sinh trả lời, false = trích đoạn tài liệu
        public bool Cached { get; set; }
    }

    public class ChatLogDto
    {
        public int Id { get; set; }
        public string Lang { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public bool UsedLlm { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
