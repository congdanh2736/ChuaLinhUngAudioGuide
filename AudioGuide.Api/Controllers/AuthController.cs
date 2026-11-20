using System.Security.Claims;
using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Auth;
using AudioGuide.Api.DTOs.Common;
using AudioGuide.Api.Interface.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AudioGuide.Api.Controllers
{
    /*
     * LỚP 1 - PRESENTATION. Quy tắc chung cho mọi Controller:
     * nhận request -> (FluentValidation tự kiểm tra dữ liệu) -> gọi Service -> trả JSON.
     * KHÔNG chứa nghiệp vụ, KHÔNG truy cập database. Lỗi do Service ném ra được ExceptionMiddleware xử lý.
     */
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _service;
        public AuthController(IAuthService service) => _service = service;

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // Đăng nhập trang quản trị -> nhận token
        [HttpPost("login")]
        public async Task<ActionResult<LoginResultDto>> Login([FromBody] LoginDto dto)
            => Ok(await _service.LoginAsync(dto));

        // Thông tin tài khoản đang đăng nhập
        [Authorize(Roles = RoleGroups.Staff)]
        [HttpGet("me")]
        public async Task<ActionResult<UserDto>> Me()
            => Ok(await _service.GetUserAsync(CurrentUserId));

        [Authorize(Roles = RoleGroups.Staff)]
        [HttpPost("change-password")]
        public async Task<ActionResult<MessageDto>> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            await _service.ChangePasswordAsync(CurrentUserId, dto);
            return Ok(new MessageDto("Đã đổi mật khẩu"));
        }

        // Đổi mật khẩu từ trang đăng nhập (không cần token): nhập tên đăng nhập + mật khẩu hiện tại + mật khẩu mới
        [HttpPost("change-password-by-login")]
        public async Task<ActionResult<MessageDto>> ChangePasswordByLogin([FromBody] ChangePasswordByLoginDto dto)
        {
            await _service.ChangePasswordByLoginAsync(dto);
            return Ok(new MessageDto("Đã đổi mật khẩu"));
        }
    }
}
