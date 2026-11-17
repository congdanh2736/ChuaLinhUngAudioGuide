namespace AudioGuide.Api.DTOs.Localization
{
    // 1 dòng trong bảng trạng thái dịch của 1 điểm (trang quản trị)
    public class LocalizationOverviewDto
    {
        public string Lang { get; set; } = string.Empty;
        public string LanguageName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;   // Pending | Processing | Ready | Failed | Outdated | Missing
        public bool IsFresh { get; set; }                     // bản dịch khớp nội dung gốc hiện tại
        public string? TranslatedBy { get; set; }
        public bool HasAudio { get; set; }
        public string? AudioUrl { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Error { get; set; }
    }

    public class LocalizeRequestDto
    {
        public List<string>? Languages { get; set; }  // bỏ trống = mọi ngôn ngữ đang bật
        public bool Force { get; set; }               // true = dịch lại kể cả bản đã có / bản sửa tay
    }

    public class LocalizeAllRequestDto
    {
        public bool Force { get; set; }
    }

    public class QueuedResultDto
    {
        public int Queued { get; set; }
        public QueuedResultDto() { }
        public QueuedResultDto(int queued) => Queued = queued;
    }

    public class DeletedResultDto
    {
        public int Deleted { get; set; }
        public DeletedResultDto() { }
        public DeletedResultDto(int deleted) => Deleted = deleted;
    }

    // Admin sửa tay bản dịch (không bị dịch máy ghi đè)
    public class ManualLocalizationDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool RegenerateAudio { get; set; } = true;
    }
}
