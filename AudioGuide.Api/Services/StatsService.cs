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

        private const int VietnamUtcOffsetHours = 7;   // Việt Nam không đổi giờ theo mùa
        private const int DefaultCodeStatsDays = 30;
        private const int MaxCodeStatsDays = 366;

        // Số mã truy cập được tạo theo ngày. Mặc định 30 ngày gần nhất; ngày không có mã vẫn trả về với số 0.
        public async Task<List<CodesByDayDto>> CodesByDayAsync(DateOnly? from, DateOnly? to)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(VietnamUtcOffsetHours));
            var end = to ?? today;
            var start = from ?? end.AddDays(-(DefaultCodeStatsDays - 1));
            if (start > end) throw new BusinessException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc", "INVALID_DATE_RANGE");
            if (end.DayNumber - start.DayNumber + 1 > MaxCodeStatsDays)
                throw new BusinessException($"Chỉ xem tối đa {MaxCodeStatsDays} ngày mỗi lần", "INVALID_DATE_RANGE");

            // Đổi khoảng ngày giờ Việt Nam -> khoảng giờ UTC để lọc trong database
            var fromUtc = start.ToDateTime(TimeOnly.MinValue).AddHours(-VietnamUtcOffsetHours);
            var toUtc = end.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-VietnamUtcOffsetHours);
            var counts = (await _sessionRepository.CountCreatedByDayAsync(fromUtc, toUtc, VietnamUtcOffsetHours))
                .ToDictionary(r => DateOnly.FromDateTime(r.Day));

            var result = new List<CodesByDayDto>();
            for (var day = end; day >= start; day = day.AddDays(-1))   // ngày mới nhất lên đầu
            {
                counts.TryGetValue(day, out var c);
                result.Add(new CodesByDayDto
                {
                    Date = day.ToString("yyyy-MM-dd"),
                    Total = c.Total, Cash = c.Cash, Online = c.Online, Activated = c.Activated
                });
            }
            return result;
        }

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
