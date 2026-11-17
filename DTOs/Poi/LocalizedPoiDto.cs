namespace AudioGuide.Api.DTOs.Poi
{
    /*
     * Điểm tham quan đã chọn đúng ngôn ngữ cho du khách (theo chuỗi dự phòng: ngôn ngữ chọn -> tiếng Anh -> tiếng Việt).
     * IsFallback = true: chưa có bản dịch đúng ngôn ngữ yêu cầu, đang hiển thị ngôn ngữ ServedLang.
     */
    public class LocalizedPoiDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int TriggerRadiusM { get; set; }
        public int Priority { get; set; }
        public string? ThumbnailUrl { get; set; }
        public List<string> ImageUrls { get; set; } = new();
        public string? AudioUrl { get; set; }
        public string RequestedLang { get; set; } = string.Empty;
        public string ServedLang { get; set; } = string.Empty;
        public bool IsFallback { get; set; }
        public double? DistanceM { get; set; }
        public List<OpeningHourDto> OpeningHours { get; set; } = new();
        public int EntryFeeVnd { get; set; }
        public int? VisitMinutes { get; set; }
        public List<string> Amenities { get; set; } = new();
        public string Tips { get; set; } = string.Empty;
        public string? TipsLang { get; set; }   // ngôn ngữ thực sự của phần lưu ý
    }
}
