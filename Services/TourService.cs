using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Poi;
using AudioGuide.Api.DTOs.Tour;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Models;
using AudioGuide.Api.Services.Logic;
using AutoMapper;

namespace AudioGuide.Api.Services
{
    // Nghiệp vụ lộ trình: quản trị lộ trình cố định + gợi ý thứ tự tham quan theo vị trí GPS
    public class TourService : ITourService
    {
        private readonly ITourRepository _tourRepository;
        private readonly IPoiRepository _poiRepository;
        private readonly IContentService _contentService;
        private readonly ILocalizationService _localizationService;
        private readonly IMapper _mapper;

        public TourService(ITourRepository tourRepository, IPoiRepository poiRepository, IContentService contentService,
            ILocalizationService localizationService, IMapper mapper)
        {
            _tourRepository = tourRepository;
            _poiRepository = poiRepository;
            _contentService = contentService;
            _localizationService = localizationService;
            _mapper = mapper;
        }

        // ------------------------------------------------------------------ quản trị
        private async Task<Tour> GetEntityAsync(int id)
            => await _tourRepository.GetWithDetailsAsync(id) ?? throw new NotFoundException("Không tìm thấy lộ trình", "TOUR_NOT_FOUND");

        public async Task<List<TourDto>> GetAllAsync()
            => _mapper.Map<List<TourDto>>(await _tourRepository.ListWithDetailsAsync(activeOnly: false));

        public async Task<TourDto> GetAsync(int id) => _mapper.Map<TourDto>(await GetEntityAsync(id));

        private async Task ValidatePoisAsync(List<int> poiIds)
        {
            if (poiIds.Distinct().Count() != poiIds.Count)
                throw new BusinessException("Một điểm không được lặp lại trong lộ trình", "DUPLICATE_POI_IN_TOUR");
            var missing = await _poiRepository.GetMissingIdsAsync(poiIds);
            if (missing.Count > 0)
                throw new BusinessException($"Điểm tham quan không tồn tại: {string.Join(", ", missing)}", "POI_NOT_FOUND");
        }

        /*
         * Ghi danh sách điểm theo thứ tự vào bảng TourPois (khóa chính = TourId + Position).
         * Dòng ở vị trí cũ thì sửa PoiId, thiếu thì thêm, thừa thì xóa
         * (không xóa hết rồi thêm lại vì EF không cho 2 bản ghi trùng khóa trong cùng 1 lần lưu).
         */
        private static void SetPois(Tour tour, List<int> poiIds)
        {
            var byPosition = tour.TourPois.ToDictionary(tp => tp.Position);
            for (var i = 0; i < poiIds.Count; i++)
            {
                if (byPosition.TryGetValue(i, out var row)) row.PoiId = poiIds[i];
                else tour.TourPois.Add(new TourPoi { Position = i, PoiId = poiIds[i] });
            }
            foreach (var extra in byPosition.Values.Where(tp => tp.Position >= poiIds.Count).ToList())
                tour.TourPois.Remove(extra);
        }

        public async Task<TourDto> CreateAsync(CreateTourDto dto)
        {
            var code = dto.Code.Trim().ToUpperInvariant();
            if (await _tourRepository.GetByCodeAsync(code) != null)
                throw new ConflictException($"Mã lộ trình '{code}' đã tồn tại", "TOUR_CODE_EXISTS");
            await ValidatePoisAsync(dto.PoiIds);
            var now = DateTime.UtcNow;
            var tour = new Tour
            {
                Code = code,
                Name = dto.Name,
                Description = dto.Description,
                EstimatedMinutes = dto.EstimatedMinutes,
                IsActive = dto.IsActive,
                CreatedAt = now,
                UpdatedAt = now
            };
            SetPois(tour, dto.PoiIds);
            await _tourRepository.AddAsync(tour);
            _localizationService.ScheduleTour(tour.Id); // dịch tên / mô tả sang các ngôn ngữ (chạy nền)
            return _mapper.Map<TourDto>(tour);
        }

        public async Task<TourDto> UpdateAsync(int id, UpdateTourDto dto)
        {
            // Kiểm tra trước khi lấy bản ghi (có theo dõi) để không lẫn dữ liệu đang sửa
            if (dto.PoiIds != null) await ValidatePoisAsync(dto.PoiIds);
            string? code = null;
            if (dto.Code != null)
            {
                code = dto.Code.Trim().ToUpperInvariant();
                var other = await _tourRepository.GetByCodeAsync(code);
                if (other != null && other.Id != id) throw new ConflictException($"Mã lộ trình '{code}' đã tồn tại", "TOUR_CODE_EXISTS");
            }

            var tour = await GetEntityAsync(id);
            var textChanged = (dto.Name != null && dto.Name != tour.Name) || (dto.Description != null && dto.Description != tour.Description);
            if (code != null) tour.Code = code;
            if (dto.Name != null) tour.Name = dto.Name;
            if (dto.Description != null) tour.Description = dto.Description.Length == 0 ? null : dto.Description;
            if (dto.EstimatedMinutes.HasValue) tour.EstimatedMinutes = dto.EstimatedMinutes.Value;
            if (dto.IsActive.HasValue) tour.IsActive = dto.IsActive.Value;
            if (dto.PoiIds != null) SetPois(tour, dto.PoiIds);
            if (textChanged) tour.Translations.Clear(); // tên / mô tả đổi -> bản dịch cũ không còn đúng
            tour.UpdatedAt = DateTime.UtcNow;
            await _tourRepository.UpdateAsync(tour);
            if (textChanged) _localizationService.ScheduleTour(id);
            return _mapper.Map<TourDto>(tour);
        }

        public async Task DeleteAsync(int id)
        {
            var tour = await GetEntityAsync(id);
            await _tourRepository.DeleteAsync(tour); // danh sách điểm + bản dịch của lộ trình bị xóa theo (CASCADE)
        }

        // ------------------------------------------------------------------ gợi ý lộ trình
        /*
         * Gợi ý thứ tự tham quan bắt đầu từ vị trí hiện tại:
         * - Có TourId: dùng các điểm của lộ trình đó.
         * - Có PoiIds: dùng các điểm khách tự chọn.
         * - Không có gì: lấy các điểm ưu tiên cao nhất.
         * Sắp thứ tự bằng "láng giềng gần nhất", khoảng cách bằng Haversine.
         */
        public async Task<RecommendedRouteDto> RecommendAsync(RecommendRequestDto dto)
        {
            var pois = await _contentService.ListPoisAsync(dto.Lang);
            var poiIds = dto.PoiIds;
            if (dto.TourId.HasValue)
                poiIds = (await GetEntityAsync(dto.TourId.Value)).TourPois.OrderBy(tp => tp.Position).Select(tp => tp.PoiId).ToList();

            if (poiIds != null && poiIds.Count > 0)
            {
                pois = pois.Where(p => poiIds.Contains(p.Id)).ToList();
                if (pois.Count == 0) throw new BusinessException("Không có điểm hợp lệ nào để tạo lộ trình", "EMPTY_ROUTE");
            }
            else
            {
                pois = pois.OrderByDescending(p => p.Priority).Take(dto.MaxStops ?? BusinessConstants.DefaultRouteStops).ToList();
            }
            if (dto.MaxStops.HasValue) pois = pois.Take(dto.MaxStops.Value).ToList();

            var ordered = Geo.NearestNeighborOrder((dto.Latitude, dto.Longitude), pois, p => (p.Latitude, p.Longitude));
            var route = new RecommendedRouteDto { StartLatitude = dto.Latitude, StartLongitude = dto.Longitude };
            var prev = (Lat: dto.Latitude, Lng: dto.Longitude);
            double totalDistance = 0, totalMinutes = 0;
            for (var i = 0; i < ordered.Count; i++)
            {
                var poi = ordered[i];
                var distance = Math.Round(Geo.HaversineM(prev.Lat, prev.Lng, poi.Latitude, poi.Longitude), 1);
                var walk = Geo.WalkingMinutes(distance);
                var listen = Geo.EstimateListenMinutes(poi.Description);
                route.Stops.Add(new RouteStopDto
                {
                    Order = i + 1,
                    Poi = poi,
                    WalkDistanceM = distance,
                    WalkMinutes = walk,
                    ListenMinutes = listen
                });
                totalDistance += distance;
                totalMinutes += walk + listen;
                prev = (poi.Latitude, poi.Longitude);
            }
            route.TotalDistanceM = Math.Round(totalDistance, 1);
            route.TotalMinutes = Math.Round(totalMinutes, 1);
            return route;
        }

        // Tổng quãng đường của 1 lộ trình khi đi đúng thứ tự admin đã xếp (không tính chặng từ vị trí du khách)
        public async Task<TourDistanceDto> GetDistanceAsync(int id)
        {
            var tour = await GetEntityAsync(id);
            var ids = tour.TourPois.OrderBy(tp => tp.Position).Select(tp => tp.PoiId).ToList();
            var byId = (await _poiRepository.ListAllAsync()).ToDictionary(p => p.Id);
            var points = ids.Where(byId.ContainsKey).Select(pid => (byId[pid].Latitude, byId[pid].Longitude)).ToList();
            return new TourDistanceDto { TourId = id, Stops = points.Count, TotalDistanceM = Geo.TotalDistanceM(points) };
        }
    }
}
