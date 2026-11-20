namespace AudioGuide.Api.DTOs.Stats
{
    // Số liệu tổng quan cho dashboard
    public class StatsDto
    {
        public int TotalPois { get; set; }
        public int ActivePois { get; set; }
        public int ActiveLanguages { get; set; }
        public Dictionary<string, int> LocalizationStatus { get; set; } = new();
        public double LocalizationCoveragePercent { get; set; }
        public Dictionary<string, int> AccessSessions { get; set; } = new();
        public long RevenueVnd { get; set; }
        public int TotalChats { get; set; }
        public int TotalEvents { get; set; }
        public List<TopPoiDto> TopPois { get; set; } = new();
        public Dictionary<string, int> EventsByLang { get; set; } = new();
        public int BackgroundTasks { get; set; }
        public string AiProvider { get; set; } = string.Empty;
    }

    // Số mã truy cập được tạo trong 1 ngày (ngày theo giờ Việt Nam)
    public class CodesByDayDto
    {
        public string Date { get; set; } = string.Empty;   // yyyy-MM-dd
        public int Total { get; set; }
        public int Cash { get; set; }                      // nhân viên bán tại quầy
        public int Online { get; set; }                    // khách mua online (đã thanh toán)
        public int Activated { get; set; }                 // trong số đó, đã được khách nhập vào app
    }

    public class TopPoiDto
    {
        public int PoiId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
