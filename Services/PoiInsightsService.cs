using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Poi;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Services.Logic;
using Microsoft.Extensions.Options;

namespace AudioGuide.Api.Services
{
    /*
     * Thông tin "sống" của 1 điểm: đang mở hay đóng cửa, giờ này có đông không.
     * Tách khỏi ContentService vì dữ liệu đổi theo từng phút -> không đưa vào bundle (bundle được cache bằng ETag).
     * TimeProvider được tiêm từ ngoài vào -> test được mọi thời điểm (8h sáng Thứ Hai, nửa đêm Chủ nhật...).
     */
    public class PoiInsightsService : IPoiInsightsService
    {
        private readonly IPoiRepository _poiRepository;
        private readonly IVisitEventRepository _eventRepository;
        private readonly TimeProvider _clock;
        private readonly int _utcOffsetHours;

        public PoiInsightsService(IPoiRepository poiRepository, IVisitEventRepository eventRepository,
            TimeProvider clock, IOptions<SiteSettings> options)
        {
            _poiRepository = poiRepository;
            _eventRepository = eventRepository;
            _clock = clock;
            _utcOffsetHours = options.Value.UtcOffsetHours;
        }

        public async Task<PoiInsightsDto> GetAsync(int poiId)
        {
            var poi = await _poiRepository.GetWithDetailsAsync(poiId);
            if (poi == null || !poi.IsActive) throw new NotFoundException("Không tìm thấy điểm tham quan", "POI_NOT_FOUND");

            var nowUtc = _clock.GetUtcNow().UtcDateTime;
            var nowLocal = nowUtc.AddHours(_utcOffsetHours);
            var since = nowUtc.AddDays(-BusinessConstants.PopularLookbackDays);
            var popular = PoiDetailsLogic.GetPopularTimes(await _eventRepository.TimesForPoiAsync(poiId, since), _utcOffsetHours);
            var status = PoiDetailsLogic.GetOpenStatus(poi.OpeningHours.ToList(), nowLocal);
            var today = PoiDetailsLogic.WeekdayIndex(nowLocal);

            return new PoiInsightsDto
            {
                PoiId = poiId,
                NowLocal = new DateTimeOffset(DateTime.SpecifyKind(nowLocal, DateTimeKind.Unspecified), TimeSpan.FromHours(_utcOffsetHours))
                    .ToString("yyyy-MM-ddTHH:mmzzz"),
                Today = today,
                OpenStatus = new OpenStatusDto
                {
                    State = status.State,
                    ClosesAt = status.ClosesAt,
                    MinutesLeft = status.MinutesLeft,
                    OpensAt = status.OpensAt,
                    OpensDay = status.OpensDay,
                    OpensInDays = status.OpensInDays
                },
                PopularTimes = new PopularTimesDto { SampleSize = popular.SampleSize, ByDay = popular.ByDay },
                BusyNow = popular.ByDay?[today][nowLocal.Hour]
            };
        }
    }
}
