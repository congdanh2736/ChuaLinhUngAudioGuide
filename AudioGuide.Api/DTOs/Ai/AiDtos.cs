namespace AudioGuide.Api.DTOs.Ai
{
    // Admin nhờ AI viết lại mô tả điểm tham quan hay hơn
    public class EnhanceRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class EnhanceResultDto
    {
        public string Description { get; set; } = string.Empty;
    }
}
