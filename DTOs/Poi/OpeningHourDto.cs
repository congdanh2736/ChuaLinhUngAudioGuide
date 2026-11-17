namespace AudioGuide.Api.DTOs.Poi
{
    // 1 khung giờ mở cửa. Mở xuyên đêm (vd 22:00 - 02:00) thì tách thành 2 khung ở 2 ngày.
    public class OpeningHourDto
    {
        public int Day { get; set; }                   // 0 = Thứ Hai ... 6 = Chủ nhật
        public string Open { get; set; } = string.Empty;  // "HH:mm"
        public string Close { get; set; } = string.Empty; // "HH:mm" hoặc "24:00"
    }
}
