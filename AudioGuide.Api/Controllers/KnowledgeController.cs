using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Knowledge;
using AudioGuide.Api.Interface.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AudioGuide.Api.Controllers
{
    // Bài viết kiến thức cho chatbot
    [ApiController]
    [Route("api/knowledge")]
    [Authorize(Roles = RoleGroups.Staff)]
    public class KnowledgeController : ControllerBase
    {
        private readonly IKnowledgeService _service;
        public KnowledgeController(IKnowledgeService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<List<ArticleDto>>> GetAll() => Ok(await _service.GetAllAsync());

        [Authorize(Roles = RoleGroups.Admin)]
        [HttpPost]
        public async Task<ActionResult<ArticleDto>> Create([FromBody] CreateArticleDto dto)
            => StatusCode(StatusCodes.Status201Created, await _service.CreateAsync(dto));

        [Authorize(Roles = RoleGroups.Admin)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<ArticleDto>> Update(int id, [FromBody] UpdateArticleDto dto) => Ok(await _service.UpdateAsync(id, dto));

        [Authorize(Roles = RoleGroups.Admin)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
    }
}
