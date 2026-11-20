using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Ai;
using AudioGuide.Api.DTOs.Chat;
using AudioGuide.Api.DTOs.Stats;
using AudioGuide.Api.Interface.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AudioGuide.Api.Controllers
{
    // Số liệu cho dashboard giám sát
    [ApiController]
    [Route("api/stats")]
    [Authorize(Roles = RoleGroups.Staff)]
    public class StatsController : ControllerBase
    {
        private readonly IStatsService _service;
        public StatsController(IStatsService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<StatsDto>> Overview() => Ok(await _service.OverviewAsync());

        // Số mã truy cập được tạo theo ngày: /api/stats/codes-by-day?from=2026-11-01&to=2026-11-20
        [HttpGet("codes-by-day")]
        public async Task<ActionResult<List<CodesByDayDto>>> CodesByDay([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
            => Ok(await _service.CodesByDayAsync(from, to));

        // Các câu hỏi gần đây của du khách
        [HttpGet("chat-logs")]
        public async Task<ActionResult<List<ChatLogDto>>> ChatLogs([FromQuery] int limit = 20)
            => Ok(await _service.RecentChatsAsync(limit));
    }

    // Trợ lý AI cho admin
    [ApiController]
    [Route("api/ai")]
    [Authorize(Roles = RoleGroups.Admin)]
    public class AiController : ControllerBase
    {
        private readonly IAiAssistantService _service;
        public AiController(IAiAssistantService service) => _service = service;

        // AI gợi ý viết lại mô tả điểm tham quan
        [HttpPost("enhance-description")]
        public async Task<ActionResult<EnhanceResultDto>> Enhance([FromBody] EnhanceRequestDto dto)
            => Ok(await _service.EnhanceDescriptionAsync(dto));
    }
}
