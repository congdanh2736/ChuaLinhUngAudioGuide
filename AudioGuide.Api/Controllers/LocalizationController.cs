using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Localization;
using AudioGuide.Api.Interface.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AudioGuide.Api.Controllers
{
    // Quản trị bản dịch + audio của từng điểm tham quan
    [ApiController]
    [Route("api/pois/{poiId:int}")]
    [Authorize(Roles = RoleGroups.Staff)]
    public class LocalizationController : ControllerBase
    {
        private readonly ILocalizationService _service;
        public LocalizationController(ILocalizationService service) => _service = service;

        // Trạng thái dịch / audio của điểm theo từng ngôn ngữ
        [HttpGet("localizations")]
        public async Task<ActionResult<List<LocalizationOverviewDto>>> Overview(int poiId) => Ok(await _service.OverviewAsync(poiId));

        // Xếp hàng dịch + tạo audio (chạy nền) -> 202 Accepted
        [Authorize(Roles = RoleGroups.Admin)]
        [HttpPost("localize")]
        public async Task<ActionResult<QueuedResultDto>> Localize(int poiId, [FromBody] LocalizeRequestDto dto)
            => Accepted(new QueuedResultDto(await _service.SchedulePoiAsync(poiId, dto.Languages, dto.Force)));

        // Sửa tay bản dịch (không bị dịch máy ghi đè)
        [Authorize(Roles = RoleGroups.Admin)]
        [HttpPut("localizations/{lang}")]
        public async Task<ActionResult<LocalizationOverviewDto>> UpdateManual(int poiId, string lang, [FromBody] ManualLocalizationDto dto)
        {
            await _service.UpdateManualAsync(poiId, lang, dto);
            return Ok((await _service.OverviewAsync(poiId)).First(r => r.Lang == lang));
        }

        // Xóa 1 bản dịch
        [Authorize(Roles = RoleGroups.Admin)]
        [HttpDelete("localizations/{lang}")]
        public async Task<IActionResult> DeleteOne(int poiId, string lang)
        {
            await _service.DeleteOneAsync(poiId, lang);
            return NoContent();
        }

        // Xóa toàn bộ bản dịch của điểm (phải làm trước khi xóa điểm)
        [Authorize(Roles = RoleGroups.Admin)]
        [HttpDelete("localizations")]
        public async Task<ActionResult<DeletedResultDto>> DeleteAll(int poiId)
            => Ok(new DeletedResultDto(await _service.DeleteForPoiAsync(poiId)));

        // Dịch lại toàn bộ điểm + lộ trình
        [Authorize(Roles = RoleGroups.Admin)]
        [HttpPost("~/api/localizations/localize-all")]
        public async Task<ActionResult<QueuedResultDto>> LocalizeAll([FromBody] LocalizeAllRequestDto dto)
            => Accepted(new QueuedResultDto(await _service.ScheduleAllAsync(dto.Force)));
    }
}
