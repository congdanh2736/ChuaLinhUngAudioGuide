using System.Security.Cryptography;
using System.Text;
using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Content;
using AudioGuide.Api.DTOs.Poi;
using AudioGuide.Api.DTOs.Tour;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Models;
using AudioGuide.Api.Services.Logic;
using AutoMapper;

namespace AudioGuide.Api.Services
{
    /*
     * Phục vụ NỘI DUNG cho du khách.
     *
     * Chuỗi dự phòng 3 tầng: ngôn ngữ yêu cầu -> tiếng Anh -> tiếng Việt gốc.
     * Khách luôn thấy nội dung, không bao giờ màn hình trống; cờ IsFallback cho app biết đang hiển thị ngôn ngữ thay thế.
     * App tải TOÀN BỘ dữ liệu 1 lần (BuildBundleAsync) kèm Version; lần sau dùng ETag, không đổi thì trả 304.
     */
    public class ContentService : IContentService
    {
        private readonly IPoiRepository _poiRepository;
        private readonly IPoiLocalizationRepository _localizationRepository;
        private readonly ITourRepository _tourRepository;
        private readonly ILanguageService _languageService;
        private readonly IMapper _mapper;

        public ContentService(IPoiRepository poiRepository, IPoiLocalizationRepository localizationRepository,
            ITourRepository tourRepository, ILanguageService languageService, IMapper mapper)
        {
            _poiRepository = poiRepository;
            _localizationRepository = localizationRepository;
            _tourRepository = tourRepository;
            _languageService = languageService;
            _mapper = mapper;
        }

        // Chuỗi ngôn ngữ thử lần lượt, bỏ trùng: vd "ja" -> ["ja", "en", "vi"]
        private List<string> Chain(string lang)
            => new[] { lang, _languageService.FallbackLang, _languageService.SourceLang }.Distinct().ToList();

        // Chọn bản dịch tốt nhất cho 1 điểm theo chuỗi dự phòng (hàm thuần, dễ test)
        public LocalizedPoiDto Localize(Poi poi, IReadOnlyDictionary<string, PoiLocalization> locs, string lang)
        {
            var source = _languageService.SourceLang;
            string name = poi.Name, description = poi.Description, served = source;
            string? audioUrl = null;
            foreach (var code in Chain(lang))
            {
                var loc = locs.GetValueOrDefault(code);
                if (LocalizationRules.IsFresh(loc, poi))
                {
                    (name, description, audioUrl, served) = (loc!.Name!, loc.Description!, loc.AudioUrl, code);
                    break;
                }
                if (code == source) break; // tầng cuối: nội dung gốc, chưa có audio khớp nội dung hiện tại
            }

            var dto = _mapper.Map<LocalizedPoiDto>(poi);
            dto.Name = name;
            dto.Description = description;
            dto.AudioUrl = audioUrl;
            dto.RequestedLang = lang;
            dto.ServedLang = served;
            dto.IsFallback = served != lang;
            (dto.Tips, dto.TipsLang) = PickTips(poi, locs, lang);
            return dto;
        }

        // Phần "lưu ý" dịch riêng (có hash riêng) nên chọn theo chuỗi dự phòng riêng
        private (string Tips, string? TipsLang) PickTips(Poi poi, IReadOnlyDictionary<string, PoiLocalization> locs, string lang)
        {
            if (string.IsNullOrEmpty(poi.Tips)) return ("", null);
            foreach (var code in Chain(lang))
            {
                if (code == _languageService.SourceLang) break;
                var loc = locs.GetValueOrDefault(code);
                if (!string.IsNullOrEmpty(loc?.Tips) && LocalizationRules.TipsFresh(loc, poi)) return (loc!.Tips!, code);
            }
            return (poi.Tips, _languageService.SourceLang);
        }

        private async Task<List<LocalizedPoiDto>> LocalizeManyAsync(List<Poi> pois, string lang)
        {
            var locs = await _localizationRepository.ListForLangsAsync(Chain(lang), pois.Select(p => p.Id));
            var grouped = locs.GroupBy(l => l.PoiId)
                .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<string, PoiLocalization>)g.ToDictionary(l => l.Lang));
            var empty = new Dictionary<string, PoiLocalization>();
            return pois.Select(p => Localize(p, grouped.GetValueOrDefault(p.Id) ?? empty, lang)).ToList();
        }

        private LocalizedTourDto LocalizeTour(Tour tour, string lang)
        {
            string name = tour.Name, served = _languageService.SourceLang;
            var description = tour.Description;
            foreach (var code in Chain(lang))
            {
                var tr = tour.Translations.FirstOrDefault(t => t.Lang == code);
                if (tr != null)
                {
                    (name, description, served) = (tr.Name, tr.Description ?? tour.Description, code);
                    break;
                }
                if (code == _languageService.SourceLang) break;
            }
            return new LocalizedTourDto
            {
                Id = tour.Id,
                Code = tour.Code,
                Name = name,
                Description = description,
                PoiIds = tour.TourPois.OrderBy(tp => tp.Position).Select(tp => tp.PoiId).ToList(),
                EstimatedMinutes = tour.EstimatedMinutes,
                ServedLang = served
            };
        }

        // ------------------------------------------------------------------ dùng cho Controller
        public async Task<List<LocalizedPoiDto>> ListPoisAsync(string lang)
        {
            lang = await _languageService.ResolveCodeAsync(lang);
            return await LocalizeManyAsync(await _poiRepository.ListActiveAsync(), lang);
        }

        public async Task<LocalizedPoiDto> GetPoiAsync(int poiId, string lang)
        {
            lang = await _languageService.ResolveCodeAsync(lang);
            var poi = await _poiRepository.GetWithDetailsAsync(poiId);
            if (poi == null || !poi.IsActive) throw new NotFoundException("Không tìm thấy điểm tham quan", "POI_NOT_FOUND");
            return (await LocalizeManyAsync(new List<Poi> { poi }, lang))[0];
        }

        // Các điểm trong bán kính, sắp xếp gần -> xa (số điểm của 1 ngôi chùa rất ít nên tính trong bộ nhớ)
        public async Task<List<LocalizedPoiDto>> NearbyAsync(double lat, double lng, double radiusM, string lang)
        {
            var items = await ListPoisAsync(lang);
            foreach (var item in items)
                item.DistanceM = Math.Round(Geo.HaversineM(lat, lng, item.Latitude, item.Longitude), 1);
            return items.Where(p => p.DistanceM <= radiusM).OrderBy(p => p.DistanceM).ToList();
        }

        public async Task<List<LocalizedTourDto>> ListToursAsync(string lang)
        {
            lang = await _languageService.ResolveCodeAsync(lang);
            return (await _tourRepository.ListWithDetailsAsync(activeOnly: true)).Select(t => LocalizeTour(t, lang)).ToList();
        }

        // Phiên bản dữ liệu = hash các mốc cập nhật mới nhất. Dữ liệu đổi -> phiên bản đổi -> ETag đổi.
        public async Task<string> ComputeVersionAsync(string lang)
        {
            var parts = new[]
            {
                lang,
                (await _poiRepository.LatestUpdateAsync())?.Ticks.ToString() ?? "",
                (await _poiRepository.CountAsync()).ToString(),
                (await _localizationRepository.LatestUpdateAsync())?.Ticks.ToString() ?? "",
                (await _tourRepository.LatestUpdateAsync())?.Ticks.ToString() ?? "",
                string.Join(",", (await _languageService.GetActiveAsync()).Select(l => l.Code))
            };
            var hash = SHA1.HashData(Encoding.UTF8.GetBytes(string.Join("|", parts)));
            return Convert.ToHexString(hash)[..16].ToLowerInvariant();
        }

        public async Task<ContentBundleDto> BuildBundleAsync(string lang)
        {
            lang = await _languageService.ResolveCodeAsync(lang);
            return new ContentBundleDto
            {
                Lang = lang,
                Version = await ComputeVersionAsync(lang),
                Languages = await _languageService.GetActiveAsync(),
                Pois = await ListPoisAsync(lang),
                Tours = await ListToursAsync(lang)
            };
        }
    }
}
