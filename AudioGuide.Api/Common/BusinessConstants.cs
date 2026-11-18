namespace AudioGuide.Api.Common
{
    // Các hằng số nghiệp vụ dùng chung
    public static class BusinessConstants
    {
        // Mã tiện ích -> nhãn tiếng Việt (nhãn các ngôn ngữ khác nằm trong chuỗi giao diện "amenity_<mã>")
        public static readonly IReadOnlyDictionary<string, string> Amenities = new Dictionary<string, string>
        {
            ["wheelchair"] = "Lối đi cho xe lăn",
            ["stairs"] = "Nhiều bậc thang",
            ["parking"] = "Bãi gửi xe",
            ["restroom"] = "Nhà vệ sinh",
            ["drinking_water"] = "Nước uống",
            ["shade"] = "Có bóng mát, ghế nghỉ",
            ["photo_ok"] = "Được chụp ảnh",
            ["no_photo_ceremony"] = "Không chụp ảnh khi hành lễ",
            ["shoes_off"] = "Bỏ giày dép trước khi vào",
            ["quiet_zone"] = "Giữ yên lặng",
            ["dress_code"] = "Trang phục kín đáo",
        };

        public static readonly string[] Categories = { "landmark", "statue", "hall", "gate", "garden", "viewpoint", "service" };

        public static readonly string[] DayNamesVi = { "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy", "Chủ nhật" };

        // Bảng chữ cho mã truy cập: bỏ ký tự dễ nhầm (0/O, 1/I/L) để khách nhập tay dễ hơn
        public const string AccessCodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        public const int AccessCodeLength = 6;
        public const int MaxCashCodesPerRequest = 50;

        // Các mức giảm giá (%) du khách được chọn khi mua vé online. 0 = không giảm
        public static readonly int[] DiscountPercents = { 0, 5, 10 };

        public const int DefaultRouteStops = 6;           // gợi ý lộ trình: số điểm mặc định
        public const double WalkingSpeedMPerMin = 60;     // đi bộ tham quan chậm ~3.6 km/h
        public const int ListenWordsPerMinute = 150;      // tốc độ đọc thuyết minh

        public const int ClosingSoonMinutes = 60;         // còn <= 60 phút là "Sắp đóng cửa"
        public const int OpeningSoonMinutes = 60;
        public const int PopularMinEvents = 20;           // ít hơn thì chưa đủ dữ liệu "khung giờ đông khách"
        public const int PopularLookbackDays = 56;        // chỉ tính 8 tuần gần nhất

        public static readonly TimeSpan FailedRetryAfter = TimeSpan.FromMinutes(5);   // bản dịch lỗi: 5 phút sau mới thử lại
        public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);        // "đang dịch" quá 10 phút -> coi như kẹt
        public static readonly TimeSpan TourRequestInterval = TimeSpan.FromMinutes(2);
    }
}
