namespace AudioGuide.Api.DTOs.Language
{
    // Ngôn ngữ cho app du khách
    public class LanguageDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NativeName { get; set; } = string.Empty;
    }

    // Ngôn ngữ cho trang quản trị (đủ thông tin)
    public class LanguageAdminDto : LanguageDto
    {
        public string TranslatorCode { get; set; } = string.Empty;
        public string TtsVoice { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
    }

    public class ToggleLanguageDto
    {
        public bool IsActive { get; set; }
    }
}
