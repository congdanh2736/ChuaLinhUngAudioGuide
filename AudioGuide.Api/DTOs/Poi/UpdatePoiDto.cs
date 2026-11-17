namespace AudioGuide.Api.DTOs.Poi
{
    // Mọi trường đều tùy chọn - chỉ gửi trường muốn sửa (null = giữ nguyên)
    public class UpdatePoiDto
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int? TriggerRadiusM { get; set; }
        public int? Priority { get; set; }
        public string? ThumbnailUrl { get; set; }    // chuỗi rỗng = xóa ảnh
        public List<string>? ImageUrls { get; set; }
        public int? SortOrder { get; set; }
        public bool? IsActive { get; set; }
        public List<OpeningHourDto>? OpeningHours { get; set; }
        public int? EntryFeeVnd { get; set; }
        public int? VisitMinutes { get; set; }
        public List<string>? Amenities { get; set; }
        public string? Tips { get; set; }
    }
}
