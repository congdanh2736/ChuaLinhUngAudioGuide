namespace AudioGuide.Api.Models
{
    // Ngôn ngữ hệ thống hỗ trợ (16 ngôn ngữ). Khóa chính Id, mã ngôn ngữ (Code) là duy nhất.
    public class Language
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;           // mã dùng trong API, vd "vi", "en", "zh"
        public string Name { get; set; } = string.Empty;           // tên tiếng Anh, vd "Vietnamese"
        public string NativeName { get; set; } = string.Empty;     // tên bản địa, vd "Tiếng Việt"
        public string TranslatorCode { get; set; } = string.Empty; // mã cho dịch máy (Google dùng "zh-CN")
        public string TtsVoice { get; set; } = string.Empty;       // giọng đọc Edge-TTS, vd "vi-VN-HoaiMyNeural"
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }

        public ICollection<PoiLocalization> PoiLocalizations { get; set; } = new List<PoiLocalization>();
        public ICollection<TourTranslation> TourTranslations { get; set; } = new List<TourTranslation>();
    }
}
