namespace AudioGuide.Api.DTOs.Poi
{
    // Thông tin "sống" của 1 điểm, tính theo giờ hiện tại: đang mở/đóng cửa, khung giờ đông khách
    public class PoiInsightsDto
    {
        public int PoiId { get; set; }
        public string NowLocal { get; set; } = string.Empty;   // giờ hiện tại ở chùa, ISO 8601
        public int Today { get; set; }                          // 0 = Thứ Hai ... 6 = Chủ nhật
        public OpenStatusDto OpenStatus { get; set; } = new();
        public PopularTimesDto PopularTimes { get; set; } = new();
        public int? BusyNow { get; set; }                       // mức đông giờ này (0-100), null nếu chưa đủ dữ liệu
    }

    public class OpenStatusDto
    {
        public string State { get; set; } = "Unknown";          // Open | ClosingSoon | Closed | OpeningSoon | Unknown
        public string? ClosesAt { get; set; }
        public int? MinutesLeft { get; set; }
        public string? OpensAt { get; set; }
        public int? OpensDay { get; set; }
        public int? OpensInDays { get; set; }
    }

    public class PopularTimesDto
    {
        public int SampleSize { get; set; }
        public Dictionary<int, int[]>? ByDay { get; set; }      // 0..6 -> 24 mức đông (0-100) theo giờ
    }
}
