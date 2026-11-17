using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Common;
using AudioGuide.Api.DTOs.Poi;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Models;
using AudioGuide.Api.Services.Logic;
using AutoMapper;

namespace AudioGuide.Api.Services
{
    // Nghiệp vụ quản trị điểm tham quan (dành cho admin)
    public class PoiService : IPoiService
    {
        private readonly IPoiRepository _poiRepository;
        private readonly IPoiLocalizationRepository _localizationRepository;
        private readonly ITourRepository _tourRepository;
        private readonly IVisitEventRepository _eventRepository;
        private readonly ILocalizationService _localizationService;
        private readonly IMapper _mapper;

        public PoiService(IPoiRepository poiRepository, IPoiLocalizationRepository localizationRepository,
            ITourRepository tourRepository, IVisitEventRepository eventRepository,
            ILocalizationService localizationService, IMapper mapper)
        {
            _poiRepository = poiRepository;
            _localizationRepository = localizationRepository;
            _tourRepository = tourRepository;
            _eventRepository = eventRepository;
            _localizationService = localizationService;
            _mapper = mapper;
        }

        public async Task<PagedResult<PoiDto>> SearchAsync(string? keyword, bool? isActive, int page, int size)
        {
            page = Math.Max(page, 1);
            size = Math.Clamp(size, 1, 100);
            var (items, total) = await _poiRepository.SearchAsync(keyword, isActive, (page - 1) * size, size);
            return new PagedResult<PoiDto> { Items = _mapper.Map<List<PoiDto>>(items), Total = total, Page = page, Size = size };
        }

        public async Task<PoiDto> GetAsync(int id)
            => _mapper.Map<PoiDto>(await GetEntityAsync(id));

        private async Task<Poi> GetEntityAsync(int id)
            => await _poiRepository.GetWithDetailsAsync(id) ?? throw new NotFoundException("Không tìm thấy điểm tham quan", "POI_NOT_FOUND");

        /*
         * Kiểm tra + chuẩn hóa thông tin chi tiết trước khi lưu (quy tắc nghiệp vụ):
         * - khung giờ không được chồng nhau, sắp theo ngày + giờ mở
         * - tiện ích phải nằm trong danh sách cho phép, bỏ trùng
         */
        private List<PoiOpeningHour> CleanHours(IEnumerable<OpeningHourDto> hours)
        {
            var list = _mapper.Map<List<PoiOpeningHour>>(hours);
            var problem = PoiDetailsLogic.FindOverlap(list);
            if (problem != null) throw new BusinessException(problem, "OPENING_HOURS_OVERLAP");
            return list.OrderBy(h => h.Day).ThenBy(h => PoiDetailsLogic.ToMinutes(h.Open)).ToList();
        }

        private static List<string> CleanAmenities(IEnumerable<string> amenities)
        {
            var list = amenities.Distinct().ToList();
            var unknown = list.Where(a => !BusinessConstants.Amenities.ContainsKey(a)).OrderBy(a => a).ToList();
            if (unknown.Count > 0) throw new BusinessException($"Tiện ích không hợp lệ: {string.Join(", ", unknown)}", "UNKNOWN_AMENITY");
            return list;
        }

        public async Task<PoiDto> CreateAsync(CreatePoiDto dto)
        {
            var poi = _mapper.Map<Poi>(dto);
            poi.Code = dto.Code.Trim().ToUpperInvariant();
            if (await _poiRepository.GetByCodeAsync(poi.Code) != null)
                throw new ConflictException($"Mã điểm '{poi.Code}' đã tồn tại", "POI_CODE_EXISTS");
            poi.OpeningHours = CleanHours(dto.OpeningHours);
            poi.Amenities = CleanAmenities(dto.Amenities);
            poi.Tips = (dto.Tips ?? "").Trim();
            poi.ThumbnailUrl = string.IsNullOrWhiteSpace(dto.ThumbnailUrl) ? null : dto.ThumbnailUrl.Trim();
            poi.CreatedAt = poi.UpdatedAt = DateTime.UtcNow;
            await _poiRepository.AddAsync(poi);

            // Tạo xong -> tự xếp hàng dịch 16 ngôn ngữ + tạo audio (chạy nền)
            await _localizationService.SchedulePoiAsync(poi.Id);
            return _mapper.Map<PoiDto>(poi);
        }

        public async Task<PoiDto> UpdateAsync(int id, UpdatePoiDto dto)
        {
            var poi = await GetEntityAsync(id);
            if (dto.Code != null)
            {
                var code = dto.Code.Trim().ToUpperInvariant();
                var other = await _poiRepository.GetByCodeAsync(code);
                if (other != null && other.Id != id) throw new ConflictException($"Mã điểm '{code}' đã tồn tại", "POI_CODE_EXISTS");
                poi.Code = code;
            }

            // Đổi tên / mô tả -> bản dịch cũ hết hiệu lực, phải dịch lại (cả audio)
            var contentChanged = (dto.Name != null && dto.Name != poi.Name) || (dto.Description != null && dto.Description != poi.Description);
            var newTips = dto.Tips?.Trim();
            var tipsChanged = newTips != null && newTips != poi.Tips;

            if (dto.Name != null) poi.Name = dto.Name;
            if (dto.Description != null) poi.Description = dto.Description;
            if (dto.Category != null) poi.Category = dto.Category;
            if (dto.Latitude.HasValue) poi.Latitude = dto.Latitude.Value;
            if (dto.Longitude.HasValue) poi.Longitude = dto.Longitude.Value;
            if (dto.TriggerRadiusM.HasValue) poi.TriggerRadiusM = dto.TriggerRadiusM.Value;
            if (dto.Priority.HasValue) poi.Priority = dto.Priority.Value;
            if (dto.ThumbnailUrl != null) poi.ThumbnailUrl = dto.ThumbnailUrl.Trim().Length == 0 ? null : dto.ThumbnailUrl.Trim();
            if (dto.ImageUrls != null) poi.ImageUrls = dto.ImageUrls;
            if (dto.SortOrder.HasValue) poi.SortOrder = dto.SortOrder.Value;
            if (dto.IsActive.HasValue) poi.IsActive = dto.IsActive.Value;
            if (dto.EntryFeeVnd.HasValue) poi.EntryFeeVnd = dto.EntryFeeVnd.Value;
            if (dto.VisitMinutes.HasValue) poi.VisitMinutes = dto.VisitMinutes.Value;
            if (dto.Amenities != null) poi.Amenities = CleanAmenities(dto.Amenities);
            if (newTips != null) poi.Tips = newTips;
            if (dto.OpeningHours != null)
            {
                var hours = CleanHours(dto.OpeningHours);
                poi.OpeningHours.Clear(); // bảng con: xóa khung giờ cũ, thêm khung giờ mới
                foreach (var h in hours) poi.OpeningHours.Add(h);
            }
            poi.UpdatedAt = DateTime.UtcNow;
            await _poiRepository.UpdateAsync(poi);

            if (contentChanged) await _localizationService.MarkOutdatedAsync(id);
            // Chỉ đổi lưu ý -> pipeline tự nhận ra, chỉ dịch lại phần lưu ý (không tạo lại audio)
            if (contentChanged || tipsChanged) await _localizationService.SchedulePoiAsync(id);
            return _mapper.Map<PoiDto>(poi);
        }

        public async Task DeleteAsync(int id)
        {
            var poi = await GetEntityAsync(id);

            // Còn bản dịch tham chiếu tới điểm -> không cho xóa (khóa ngoại cũng chặn, kiểm tra trước để báo lỗi dễ hiểu)
            var remaining = await _localizationRepository.CountForPoiAsync(id);
            if (remaining > 0)
                throw new ConflictException($"Không thể xóa điểm vì còn {remaining} bản dịch tham chiếu tới. Hãy xóa các bản dịch trước.",
                    "POI_HAS_LOCALIZATIONS", new { localizations = remaining });

            // Điểm đã có lượt tham quan được thống kê -> chỉ nên ẩn, không xóa
            var visits = await _eventRepository.CountForPoiAsync(id);
            if (visits > 0)
                throw new ConflictException($"Không thể xóa điểm vì đã có {visits} lượt tham quan được thống kê. Hãy ẩn điểm (tắt hiển thị) thay vì xóa.",
                    "POI_HAS_VISIT_EVENTS", new { visitEvents = visits });

            // Gỡ điểm khỏi các lộ trình rồi mới xóa (khóa ngoại vẫn là chốt chặn cuối -> 409)
            await _tourRepository.RemovePoiEverywhereAsync(id);
            await _poiRepository.DeleteAsync(poi);
        }
    }
}
