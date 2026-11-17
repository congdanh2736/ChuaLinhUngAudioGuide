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

    public class TopPoiDto
    {
        public int PoiId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
