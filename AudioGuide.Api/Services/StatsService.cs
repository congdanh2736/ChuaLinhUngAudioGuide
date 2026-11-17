using AudioGuide.Api.BackgroundJobs;
using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Chat;
using AudioGuide.Api.DTOs.Content;
using AudioGuide.Api.DTOs.Stats;
using AudioGuide.Api.Interface.Integrations;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Models;
using AutoMapper;

namespace AudioGuide.Api.Services
{
    // Thống kê cho dashboard + ghi nhận hành vi sử dụng của du khách
    public class StatsService : IStatsService
    {
        private readonly IPoiRepository _poiRepository;
        private readonly IPoiLocalizationRepository _localizationRepository;
        private readonly ILanguageRepository _languageRepository;
        private readonly IAccessSessionRepository _sessionRepository;
        private readonly IChatLogRepository _chatRepository;
        private readonly IVisitEventRepository _eventRepository;
        private readonly IBackgroundTaskQueue _queue;
        private readonly ILlmClient _llm;
        private readonly IMapper _mapper;

        public StatsService(IPoiRepository poiRepository, IPoiLocalizationRepository localizationRepository,
            ILanguageRepository languageRepository, IAccessSessionRepository sessionRepository, IChatLogRepository chatRepository,
            IVisitEventRepository eventRepository, IBackgroundTaskQueue queue, ILlmClient llm, IMapper mapper)
        {
            _poiRepository = poiRepository;
            _localizationRepository = localizationRepository;
            _languageRepository = languageRepository;
            _sessionRepository = sessionRepository;
            _chatRepository = chatRepository;
            _eventRepository = eventRepository;
            _queue = queue;
            _llm = llm;
            _mapper = mapper;
        }

        public async Task RecordEventAsync(EventRequestDto dto, int? sessionId)
        {
            if (await _poiRepository.GetByIdAsync(dto.PoiId) == null)
                throw new NotFoundException("Không tìm thấy điểm tham quan", "POI_NOT_FOUND");
            await _eventRepository.AddAsync(new VisitEvent
            {
                PoiId = dto.PoiId,
                Type = dto.Type,
                Lang = dto.Lang,
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow
            });
        }

        public async Task<List<ChatLogDto>> RecentChatsAsync(int limit)
            => _mapper.Map<List<ChatLogDto>>(await _chatRepository.RecentAsync(Math.Clamp(limit, 1, 100)));

        public async Task<StatsDto> OverviewAsync()
        {
            await _sessionRepository.MarkExpiredAsync(DateTime.UtcNow); // đếm theo trạng thái đúng thời điểm hiện tại
            var totalPois = await _poiRepository.CountAsync();
            var activeLanguages = await _languageRepository.CountActiveAsync();
            var locStatus = await _localizationRepository.CountByStatusAsync();
            var expected = totalPois * activeLanguages;
            var ready = locStatus.GetValueOrDefault(nameof(LocalizationStatus.Ready));
            var coverage = expected > 0 ? Math.Round(100.0 * ready / expected, 1) : 0;

            var top = new List<TopPoiDto>();
            foreach (var (poiId, count) in await _eventRepository.TopPoisAsync(5))
            {
                var poi = await _poiRepository.GetByIdAsync(poiId);
                top.Add(new TopPoiDto { PoiId = poiId, Name = poi?.Name ?? "(đã xóa)", Count = count });
            }

            return new StatsDto
            {
                TotalPois = totalPois,
                ActivePois = await _poiRepository.CountActiveAsync(),
                ActiveLanguages = activeLanguages,
                LocalizationStatus = locStatus,
                LocalizationCoveragePercent = Math.Min(coverage, 100),
                AccessSessions = await _sessionRepository.CountByStatusAsync(),
                RevenueVnd = await _sessionRepository.TotalRevenueAsync(),
                TotalChats = await _chatRepository.CountAsync(),
                TotalEvents = await _eventRepository.CountAsync(),
                TopPois = top,
                EventsByLang = await _eventRepository.CountByLangAsync(),
                BackgroundTasks = _queue.PendingCount,
                AiProvider = _llm.Describe
            };
        }
    }
}
