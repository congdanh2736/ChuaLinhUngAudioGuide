namespace AudioGuide.Api.Models
{
    /*
     * Điểm tham quan (Point of Interest). Nội dung gốc luôn là tiếng Việt.
     * Bản dịch sang các ngôn ngữ khác nằm ở bảng PoiLocalizations.
     */
    public class Poi
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;          // mã duy nhất, vd "CONG_TAM_QUAN"
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;   // nội dung thuyết minh (tiếng Việt)
        public string Category { get; set; } = "landmark";        // gate | statue | hall | garden | viewpoint | landmark | service
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int TriggerRadiusM { get; set; } = 30;             // bán kính geofence: vào vùng này app tự phát audio
        public int Priority { get; set; }                         // nhiều vùng chồng nhau: số lớn được ưu tiên
        public string? ThumbnailUrl { get; set; }
        public List<string> ImageUrls { get; set; } = new();      // EF lưu thành cột JSON
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;

        // --- Thông tin chi tiết (giống thẻ địa điểm trên Google Maps) ---
        public int EntryFeeVnd { get; set; }                      // 0 = miễn phí
        public int? VisitMinutes { get; set; }                    // thời gian tham quan gợi ý
        public List<string> Amenities { get; set; } = new();      // mã tiện ích, xem Common/BusinessConstants.Amenities
        public string Tips { get; set; } = string.Empty;          // lưu ý cho du khách (tiếng Việt, được dịch như mô tả)

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<PoiOpeningHour> OpeningHours { get; set; } = new List<PoiOpeningHour>();
        public ICollection<PoiLocalization> Localizations { get; set; } = new List<PoiLocalization>();
        public ICollection<TourPoi> TourPois { get; set; } = new List<TourPoi>();
        public ICollection<VisitEvent> VisitEvents { get; set; } = new List<VisitEvent>();
    }

    /*
     * 1 khung giờ mở cửa trong 1 ngày. Một ngày có thể có nhiều khung (vd nghỉ trưa).
     * Bảng con thuộc sở hữu của Poi: xóa / sửa POI thì các khung giờ đi theo.
     */
    public class PoiOpeningHour
    {
        public int Id { get; set; }
        public int PoiId { get; set; }
        public int Day { get; set; }                       // 0 = Thứ Hai ... 6 = Chủ nhật
        public string Open { get; set; } = "00:00";        // "HH:mm"
        public string Close { get; set; } = "24:00";       // "HH:mm", "24:00" = tới nửa đêm

        public Poi? Poi { get; set; }
    }
}
