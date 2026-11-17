using System.Security.Claims;
using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Auth;
using AudioGuide.Api.Interface.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AudioGuide.Api.Controllers
{
    // Quản lý tài khoản nhân viên - chỉ Admin
    [ApiController]
    [Route("api/users")]
    [Authorize(Roles = RoleGroups.Admin)]
    public class UserController : ControllerBase
    {
        private readonly IAuthService _service;
        public UserController(IAuthService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<List<UserDto>>> GetAll() => Ok(await _service.ListUsersAsync());

        [HttpPost]
        public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserDto dto)
        {
            var user = await _service.CreateUserAsync(dto);
            return StatusCode(StatusCodes.Status201Created, user);
        }

        [HttpPatch("{id}")]
        public async Task<ActionResult<UserDto>> Update(string id, [FromBody] UpdateUserDto dto)
            => Ok(await _service.UpdateUserAsync(id, dto, User.FindFirstValue(ClaimTypes.NameIdentifier)!));
    }
}
