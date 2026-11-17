using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Language;
using AudioGuide.Api.Interface.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AudioGuide.Api.Controllers
{
    [ApiController]
    [Route("api/languages")]
    public class LanguageController : ControllerBase
    {
        private readonly ILanguageService _service;
        public LanguageController(ILanguageService service) => _service = service;

        // Công khai: các ngôn ngữ đang bật (app du khách hiện ô chọn ngôn ngữ)
        [HttpGet]
        public async Task<ActionResult<List<LanguageDto>>> GetActive() => Ok(await _service.GetActiveAsync());

        // Quản trị: tất cả ngôn ngữ kèm mã dịch máy, giọng đọc
        [Authorize(Roles = RoleGroups.Staff)]
        [HttpGet("all")]
        public async Task<ActionResult<List<LanguageAdminDto>>> GetAll() => Ok(await _service.GetAllAsync());

        // Bật / tắt ngôn ngữ (không tắt được tiếng Việt và tiếng Anh)
        [Authorize(Roles = RoleGroups.Admin)]
        [HttpPatch("{code}")]
        public async Task<ActionResult<LanguageAdminDto>> Toggle(string code, [FromBody] ToggleLanguageDto dto)
            => Ok(await _service.SetActiveAsync(code, dto.IsActive));
    }
}
