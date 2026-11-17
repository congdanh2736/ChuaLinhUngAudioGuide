namespace AudioGuide.Api.Common
{
    /*
     * Cấu hình đọc từ appsettings.json (mỗi lớp = 1 mục trong file).
     * Đổi giá trị trong appsettings.json, không phải sửa code.
     */
    public class JwtSettings
    {
        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = "AudioGuideApi";
        public string Audience { get; set; } = "AudioGuideClient";
        public int AdminTokenMinutes { get; set; } = 480;
    }

    public class AccessSettings
    {
        public bool Required { get; set; } = true;       // false = mở tự do, tiện khi demo
        public int PriceVnd { get; set; } = 50000;
        public int AccessHours { get; set; } = 24;       // thời gian dùng app sau khi đổi mã
        public int CodeTtlHours { get; set; } = 24;      // mã chưa dùng hết hạn sau ngần này giờ
    }

    public class PaymentSettings
    {
        public string Provider { get; set; } = "mock";
        public string WebhookSecret { get; set; } = "mock-webhook-secret";
    }

    public class LocalizationSettings
    {
        public string SourceLanguage { get; set; } = "vi";   // ngôn ngữ gốc admin nhập nội dung
        public string FallbackLanguage { get; set; } = "en"; // ngôn ngữ dự phòng cho khách quốc tế
        public int MaxConcurrentJobs { get; set; } = 3;      // số tác vụ dịch chạy nền cùng lúc
    }

    public class TranslatorSettings
    {
        public string Provider { get; set; } = "google";    // google (Google -> MyMemory) | mymemory | none
        public string? MyMemoryEmail { get; set; }
    }

    public class TtsSettings
    {
        public string Provider { get; set; } = "edge";      // edge | none
    }

    public class LlmSettings
    {
        public string Provider { get; set; } = "auto";      // auto | openrouter | gemini | groq | none
        public string? ApiKey { get; set; }
        public string? Model { get; set; }                  // trống = model mặc định của nhà cung cấp
        public string? BaseUrl { get; set; }
        public int TimeoutSeconds { get; set; } = 60;
    }

    public class ChatSettings
    {
        public int DailyLimit { get; set; } = 50;
        public int CacheTtlSeconds { get; set; } = 600;
        public int HistoryTurns { get; set; } = 4;
    }

    public class SiteSettings
    {
        public int UtcOffsetHours { get; set; } = 7;        // múi giờ của chùa (Việt Nam UTC+7)
    }

    public class SeedSettings
    {
        public string AdminUserName { get; set; } = "admin";
        public string AdminPassword { get; set; } = "admin123";
        public bool SampleData { get; set; } = true;
    }
}
