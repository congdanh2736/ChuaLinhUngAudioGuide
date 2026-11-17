using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Language;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Models;
using AutoMapper;
using Microsoft.Extensions.Options;

namespace AudioGuide.Api.Services
{
    // Nghiệp vụ ngôn ngữ: liệt kê, bật/tắt, chọn ngôn ngữ dự phòng
    public class LanguageService : ILanguageService
    {
        private readonly ILanguageRepository _repository;
        private readonly IMapper _mapper;
        private readonly LocalizationSettings _settings;

        public LanguageService(ILanguageRepository repository, IMapper mapper, IOptions<LocalizationSettings> options)
        {
            _repository = repository;
            _mapper = mapper;
            _settings = options.Value;
        }

        public string SourceLang => _settings.SourceLanguage;
        public string FallbackLang => _settings.FallbackLanguage;

        public async Task<List<LanguageDto>> GetActiveAsync()
            => _mapper.Map<List<LanguageDto>>(await _repository.ListAsync(activeOnly: true));

        public async Task<List<LanguageAdminDto>> GetAllAsync()
            => _mapper.Map<List<LanguageAdminDto>>(await _repository.ListAsync(activeOnly: false));

        // Lấy ngôn ngữ đang bật; không hỗ trợ thì báo lỗi
        public async Task<Language> GetActiveEntityAsync(string code)
        {
            var language = await _repository.GetByCodeAsync(code);
            if (language == null || !language.IsActive)
                throw new NotFoundException($"Ngôn ngữ '{code}' không được hỗ trợ", "LANGUAGE_NOT_SUPPORTED");
            return language;
        }

        // Ngôn ngữ không hợp lệ / đang tắt -> dùng ngôn ngữ dự phòng thay vì báo lỗi (thân thiện với app)
        public async Task<string> ResolveCodeAsync(string? code)
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                var language = await _repository.GetByCodeAsync(code.Trim());
                if (language != null && language.IsActive) return language.Code;
            }
            return FallbackLang;
        }

        public async Task<LanguageAdminDto> SetActiveAsync(string code, bool isActive)
        {
            if (!isActive && (code == SourceLang || code == FallbackLang))
                throw new BusinessException("Không thể tắt ngôn ngữ gốc hoặc ngôn ngữ dự phòng", "LANGUAGE_REQUIRED");
            var language = await _repository.GetByCodeAsync(code)
                           ?? throw new NotFoundException($"Không tìm thấy ngôn ngữ '{code}'", "LANGUAGE_NOT_FOUND");
            language.IsActive = isActive;
            await _repository.UpdateAsync(language);
            return _mapper.Map<LanguageAdminDto>(language);
        }
    }
}
