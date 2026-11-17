using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Tour;
using AudioGuide.Api.Interface.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AudioGuide.Api.Controllers
{
    // Quản trị lộ trình
    [ApiController]
    [Route("api/tours")]
    [Authorize(Roles = RoleGroups.Staff)]
    public class TourController : ControllerBase
    {
        private readonly ITourService _service;
        public TourController(ITourService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<List<TourDto>>> GetAll() => Ok(await _service.GetAllAsync());

        [HttpGet("{id:int}")]
        public async Task<ActionResult<TourDto>> GetById(int id) => Ok(await _service.GetAsync(id));

        // Tổng quãng đường khi đi đúng thứ tự các điểm của lộ trình (công thức Haversine)
        [HttpGet("{id:int}/distance")]
        public async Task<ActionResult<TourDistanceDto>> Distance(int id) => Ok(await _service.GetDistanceAsync(id));

        [Authorize(Roles = RoleGroups.Admin)]
        [HttpPost]
        public async Task<ActionResult<TourDto>> Create([FromBody] CreateTourDto dto)
        {
            var tour = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = tour.Id }, tour);
        }

        [Authorize(Roles = RoleGroups.Admin)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<TourDto>> Update(int id, [FromBody] UpdateTourDto dto) => Ok(await _service.UpdateAsync(id, dto));

        [Authorize(Roles = RoleGroups.Admin)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
    }
}
