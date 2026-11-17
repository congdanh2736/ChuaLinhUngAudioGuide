using AudioGuide.Api.DTOs.Poi;

namespace AudioGuide.Api.DTOs.Tour
{
    public class TourDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<int> PoiIds { get; set; } = new();          // các điểm theo thứ tự
        public int EstimatedMinutes { get; set; }
        public bool IsActive { get; set; }
        public List<string> TranslatedLanguages { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateTourDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<int> PoiIds { get; set; } = new();
        public int EstimatedMinutes { get; set; } = 30;
        public bool IsActive { get; set; } = true;
    }

    public class UpdateTourDto
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public List<int>? PoiIds { get; set; }
        public int? EstimatedMinutes { get; set; }
        public bool? IsActive { get; set; }
    }

    // Lộ trình đã chọn đúng ngôn ngữ cho du khách
    public class LocalizedTourDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<int> PoiIds { get; set; } = new();
        public int EstimatedMinutes { get; set; }
        public string ServedLang { get; set; } = string.Empty;
    }

    // Du khách xin gợi ý thứ tự tham quan từ vị trí hiện tại
    public class RecommendRequestDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Lang { get; set; } = "vi";
        public List<int>? PoiIds { get; set; }   // các điểm khách tự chọn (bỏ trống = hệ thống tự gợi ý)
        public int? TourId { get; set; }         // dùng các điểm của 1 lộ trình có sẵn
        public int? MaxStops { get; set; }
    }

    public class RouteStopDto
    {
        public int Order { get; set; }
        public LocalizedPoiDto Poi { get; set; } = new();
        public double WalkDistanceM { get; set; }   // quãng đường đi bộ từ điểm trước tới điểm này
        public double WalkMinutes { get; set; }
        public double ListenMinutes { get; set; }   // thời gian nghe thuyết minh ước tính
    }

    public class RecommendedRouteDto
    {
        public double StartLatitude { get; set; }
        public double StartLongitude { get; set; }
        public List<RouteStopDto> Stops { get; set; } = new();
        public double TotalDistanceM { get; set; }
        public double TotalMinutes { get; set; }
    }

    // Tổng quãng đường của 1 lộ trình khi đi đúng thứ tự admin đã xếp
    public class TourDistanceDto
    {
        public int TourId { get; set; }
        public int Stops { get; set; }
        public double TotalDistanceM { get; set; }
    }
}
