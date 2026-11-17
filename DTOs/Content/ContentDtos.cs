using AudioGuide.Api.DTOs.Language;
using AudioGuide.Api.DTOs.Poi;
using AudioGuide.Api.DTOs.Tour;
using AudioGuide.Api.Models;

namespace AudioGuide.Api.DTOs.Content
{
    // TOÀN BỘ dữ liệu app du khách cần, tải 1 lần (lần sau dùng ETag, không đổi thì server trả 304)
    public class ContentBundleDto
    {
        public string Lang { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public List<LanguageDto> Languages { get; set; } = new();
        public List<LocalizedPoiDto> Pois { get; set; } = new();
        public List<LocalizedTourDto> Tours { get; set; } = new();
        public int PendingTranslations { get; set; }   // > 0: hệ thống đang dịch, app nên tải lại sau vài giây
    }

    public class UiStringsDto
    {
        public string Lang { get; set; } = string.Empty;
        public Dictionary<string, string> Strings { get; set; } = new();
        public bool IsFallback { get; set; }
    }

    // Ghi nhận hành vi du khách (xem điểm, nghe audio, vào vùng geofence)
    public class EventRequestDto
    {
        public int PoiId { get; set; }
        public EventType Type { get; set; }
        public string Lang { get; set; } = "vi";
    }
}
