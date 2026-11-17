using AudioGuide.Api.DTOs.Chat;
using AudioGuide.Api.DTOs.Common;
using AudioGuide.Api.DTOs.Content;
using AudioGuide.Api.DTOs.Poi;
using AudioGuide.Api.DTOs.Tour;
using AudioGuide.Api.Filters;
using AudioGuide.Api.Interface.Services;
using Microsoft.AspNetCore.Mvc;

namespace AudioGuide.Api.Controllers
{
    // API nội dung cho app du khách (cần token du khách - [VisitorAccess])
    [ApiController]
    [Route("api/content")]
    [VisitorAccess]
    public class ContentController : ControllerBase
    {
        private readonly IContentService _contentService;
        private readonly ILanguageService _languageService;
        private readonly ILocalizationService _localizationService;
        private readonly IPoiInsightsService _insightsService;
        private readonly ITourService _tourService;
        private readonly IStatsService _statsService;

        public ContentController(IContentService contentService, ILanguageService languageService, ILocalizationService localizationService,
            IPoiInsightsService insightsService, ITourService tourService, IStatsService statsService)
        {
            _contentService = contentService;
            _languageService = languageService;
            _localizationService = localizationService;
            _insightsService = insightsService;
            _tourService = tourService;
            _statsService = statsService;
        }

        /*
         * Tải TOÀN BỘ dữ liệu 1 lần. Hỗ trợ ETag: app gửi lại ETag cũ qua header If-None-Match,
         * dữ liệu không đổi thì trả 304 (không gửi lại nội dung) -> tiết kiệm mạng.
         * Khách chọn ngôn ngữ chưa dịch -> xếp hàng dịch ngay ở nền; header X-Translation-Pending cho app biết còn bao nhiêu điểm đang dịch.
         */
        [HttpGet("bundle")]
        public async Task<IActionResult> Bundle([FromQuery] string lang = "vi")
        {
            var resolved = await _languageService.ResolveCodeAsync(lang);
            var pending = await _localizationService.RequestLanguageAsync(resolved);
            var version = await _contentService.ComputeVersionAsync(resolved);
            var etag = $"W/\"{version}\"";
            Response.Headers.ETag = etag;
            Response.Headers["X-Translation-Pending"] = pending.ToString();
            Response.Headers.CacheControl = "no-cache";
            if (Request.Headers.IfNoneMatch.ToString() == etag) return StatusCode(StatusCodes.Status304NotModified);
            var bundle = await _contentService.BuildBundleAsync(resolved);
            bundle.PendingTranslations = pending;
            return Ok(bundle);
        }

        [HttpGet("pois")]
        public async Task<ActionResult<List<LocalizedPoiDto>>> Pois([FromQuery] string lang = "vi")
            => Ok(await _contentService.ListPoisAsync(lang));

        // Các điểm gần vị trí GPS, sắp xếp gần -> xa
        [HttpGet("pois/nearby")]
        public async Task<ActionResult<List<LocalizedPoiDto>>> Nearby([FromQuery] double lat, [FromQuery] double lng,
            [FromQuery] double radius = 300, [FromQuery] string lang = "vi")
            => Ok(await _contentService.NearbyAsync(lat, lng, Math.Clamp(radius, 10, 5000), lang));

        [HttpGet("pois/{id:int}")]
        public async Task<ActionResult<LocalizedPoiDto>> Poi(int id, [FromQuery] string lang = "vi")
            => Ok(await _contentService.GetPoiAsync(id, lang));

        // Đang mở / đóng cửa + khung giờ đông khách (đổi theo giờ, không cache)
        [HttpGet("pois/{id:int}/insights")]
        public async Task<ActionResult<PoiInsightsDto>> Insights(int id)
        {
            Response.Headers.CacheControl = "no-store";
            return Ok(await _insightsService.GetAsync(id));
        }

        // Dịch + tạo audio NGAY cho 1 ngôn ngữ chưa có (khách tới gần điểm mà chưa có bản dịch)
        [HttpPost("pois/{id:int}/localizations/{lang}/ensure")]
        public async Task<ActionResult<LocalizedPoiDto>> Ensure(int id, string lang)
        {
            await _languageService.GetActiveEntityAsync(lang);
            await _localizationService.EnsureAsync(id, lang);
            return Ok(await _contentService.GetPoiAsync(id, lang));
        }

        [HttpGet("tours")]
        public async Task<ActionResult<List<LocalizedTourDto>>> Tours([FromQuery] string lang = "vi")
            => Ok(await _contentService.ListToursAsync(lang));

        // Gợi ý thứ tự tham quan từ vị trí hiện tại
        [HttpPost("tours/recommend")]
        public async Task<ActionResult<RecommendedRouteDto>> Recommend([FromBody] RecommendRequestDto dto)
            => Ok(await _tourService.RecommendAsync(dto));

        // Ghi nhận hành vi (xem điểm, nghe audio, vào vùng geofence) để thống kê
        [HttpPost("events")]
        public async Task<ActionResult<MessageDto>> RecordEvent([FromBody] EventRequestDto dto)
        {
            await _statsService.RecordEventAsync(dto, HttpContext.GetVisitor().SessionId);
            return StatusCode(StatusCodes.Status201Created, new MessageDto("Đã ghi nhận"));
        }
    }

    // Chatbot hỏi đáp
    [ApiController]
    [Route("api/chat")]
    [VisitorAccess]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _service;
        public ChatController(IChatService service) => _service = service;

        [HttpPost]
        public async Task<ActionResult<ChatAnswerDto>> Ask([FromBody] ChatRequestDto dto)
            => Ok(await _service.AskAsync(dto.Question, dto.Lang, HttpContext.GetVisitor().SessionId));
    }

    // Chuỗi giao diện app du khách theo ngôn ngữ (công khai)
    [ApiController]
    [Route("api/ui-strings")]
    public class UiStringsController : ControllerBase
    {
        private readonly IUiTextService _service;
        public UiStringsController(IUiTextService service) => _service = service;

        [HttpGet("{lang}")]
        public async Task<ActionResult<UiStringsDto>> Get(string lang) => Ok(await _service.GetStringsAsync(lang));
    }
}
