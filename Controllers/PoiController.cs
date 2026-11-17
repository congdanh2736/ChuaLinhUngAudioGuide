using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Common;
using AudioGuide.Api.DTOs.Poi;
using AudioGuide.Api.Interface.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AudioGuide.Api.Controllers
{
    // Quản trị điểm tham quan: nhân viên xem, admin thêm / sửa / xóa
    [ApiController]
    [Route("api/pois")]
    [Authorize(Roles = RoleGroups.Staff)]
    public class PoiController : ControllerBase
    {
        private readonly IPoiService _service;
        public PoiController(IPoiService service) => _service = service;

        // Tìm theo tên / mã, lọc trạng thái, phân trang
        [HttpGet]
        public async Task<ActionResult<PagedResult<PoiDto>>> Search([FromQuery] string? q, [FromQuery] bool? isActive,
            [FromQuery] int page = 1, [FromQuery] int size = 20)
            => Ok(await _service.SearchAsync(q, isActive, page, size));

        [HttpGet("{id:int}")]
        public async Task<ActionResult<PoiDto>> GetById(int id) => Ok(await _service.GetAsync(id));

        // Tạo điểm -> tự xếp hàng dịch 16 ngôn ngữ + tạo audio (chạy nền)
        [Authorize(Roles = RoleGroups.Admin)]
        [HttpPost]
        public async Task<ActionResult<PoiDto>> Create([FromBody] CreatePoiDto dto)
        {
            var poi = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = poi.Id }, poi);
        }

        // Sửa điểm (đổi tên / mô tả -> tự dịch lại)
        [Authorize(Roles = RoleGroups.Admin)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<PoiDto>> Update(int id, [FromBody] UpdatePoiDto dto) => Ok(await _service.UpdateAsync(id, dto));

        // Xóa điểm (bị chặn nếu còn bản dịch hoặc đã có lượt tham quan; tự gỡ khỏi các lộ trình)
        [Authorize(Roles = RoleGroups.Admin)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
    }
}
