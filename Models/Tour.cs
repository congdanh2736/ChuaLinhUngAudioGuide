namespace AudioGuide.Api.Models
{
    // Lộ trình gợi ý gồm nhiều điểm tham quan theo thứ tự
    public class Tour
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int EstimatedMinutes { get; set; } = 30;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<TourPoi> TourPois { get; set; } = new List<TourPoi>();               // các điểm theo thứ tự
        public ICollection<TourTranslation> Translations { get; set; } = new List<TourTranslation>();
    }

    // Bảng trung gian N-N: lộ trình - điểm tham quan, có thứ tự (Position)
    public class TourPoi
    {
        public int TourId { get; set; }
        public int Position { get; set; }
        public int PoiId { get; set; }

        public Tour? Tour { get; set; }
        public Poi? Poi { get; set; }
    }

    // Bản dịch tên / mô tả lộ trình theo ngôn ngữ
    public class TourTranslation
    {
        public int TourId { get; set; }
        public string Lang { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        public Tour? Tour { get; set; }
        public Language? Language { get; set; }
    }
}
