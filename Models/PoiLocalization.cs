namespace AudioGuide.Api.Models
{
    /*
     * Bản dịch + audio của 1 điểm tham quan cho 1 ngôn ngữ. Mỗi (PoiId, Lang) chỉ có 1 dòng.
     * SourceHash = MD5(tên + mô tả gốc) lúc dịch -> admin sửa nội dung gốc thì bản dịch cũ tự hết hiệu lực.
     */
    public class PoiLocalization
    {
        public int Id { get; set; }
        public int PoiId { get; set; }
        public string Lang { get; set; } = string.Empty;     // khóa ngoại tới Languages.Code
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? AudioUrl { get; set; }                // vd "/media/audio/<md5>.mp3"
        public LocalizationStatus Status { get; set; } = LocalizationStatus.Pending;
        public TranslatedBy TranslatedBy { get; set; } = TranslatedBy.Machine;
        public string? SourceHash { get; set; }
        public string? Tips { get; set; }                    // bản dịch phần "lưu ý"
        public string? TipsHash { get; set; }                // hash phần lưu ý gốc lúc dịch
        public string? Error { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Poi? Poi { get; set; }
        public Language? Language { get; set; }
    }
}
