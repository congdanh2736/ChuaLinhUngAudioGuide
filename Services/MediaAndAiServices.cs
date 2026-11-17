using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Ai;
using AudioGuide.Api.Interface.Integrations;
using AudioGuide.Api.Interface.Services;

namespace AudioGuide.Api.Services
{
    // Phục vụ file audio cho app (Controller không gọi thẳng nơi lưu file)
    public class MediaService : IMediaService
    {
        private readonly IMediaStorage _storage;
        public MediaService(IMediaStorage storage) => _storage = storage;

        public async Task<(byte[] Data, string ContentType)> GetAsync(string key)
            => await _storage.GetAsync(key) ?? throw new NotFoundException("Không tìm thấy file", "MEDIA_NOT_FOUND");
    }

    // Trợ lý AI cho admin: gợi ý viết lại mô tả điểm tham quan hay hơn
    public class AiAssistantService : IAiAssistantService
    {
        private readonly ILlmClient _llm;
        public AiAssistantService(ILlmClient llm) => _llm = llm;

        public async Task<EnhanceResultDto> EnhanceDescriptionAsync(EnhanceRequestDto dto)
        {
            if (!_llm.IsEnabled)
                throw new ExternalServiceException("Chưa cấu hình AI (thiếu Llm:ApiKey trong appsettings)", "LLM_NOT_CONFIGURED");
            var messages = new List<LlmMessage>
            {
                new("system", "Bạn là biên tập viên nội dung thuyết minh du lịch tâm linh. Viết lại mô tả tiếng Việt cho giọng đọc "
                              + "thuyết minh: tự nhiên, trang nghiêm, 120-200 từ. TUYỆT ĐỐI không thêm số liệu hay sự kiện không có "
                              + "trong bản gốc. Chỉ trả về đoạn văn, không tiêu đề, không Markdown."),
                new("user", $"Điểm tham quan: {dto.Name}\nMô tả gốc: {dto.Description}")
            };
            return new EnhanceResultDto { Description = await _llm.CompleteAsync(messages, temperature: 0.5) };
        }
    }
}
