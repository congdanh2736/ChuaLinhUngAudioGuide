using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AudioGuide.Api.Common;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AudioGuide.Api.Services
{
    /*
     * Tạo token JWT. Hệ thống có 2 loại token, cùng ký bằng khóa bí mật Jwt:Key:
     *  - Token quản trị: cấp khi admin / nhân viên đăng nhập, chứa vai trò Admin hoặc Staff.
     *  - Token du khách: cấp khi du khách đổi mã truy cập, chứa vai trò Visitor + id phiên truy cập.
     * Middleware JwtBearer tự kiểm tra chữ ký + hạn dùng; [Authorize(Roles=...)] tự kiểm tra vai trò.
     */
    public class JwtService : IJwtService
    {
        public const string SessionClaim = "session_id";
        private readonly JwtSettings _settings;
        private readonly UserManager<ApplicationUser> _userManager;

        public JwtService(IOptions<JwtSettings> options, UserManager<ApplicationUser> userManager)
        {
            _settings = options.Value;
            _userManager = userManager;
        }

        private string Write(IEnumerable<Claim> claims, DateTime expiresAt)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<(string Token, DateTime ExpiresAt)> CreateAdminTokenAsync(ApplicationUser user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName ?? "")
            };
            foreach (var role in await _userManager.GetRolesAsync(user))
                claims.Add(new Claim(ClaimTypes.Role, role));
            var expiresAt = DateTime.UtcNow.AddMinutes(_settings.AdminTokenMinutes);
            return (Write(claims, expiresAt), expiresAt);
        }

        public string CreateVisitorToken(int sessionId, DateTime expiresAt)
        {
            var claims = new List<Claim>
            {
                new(SessionClaim, sessionId.ToString()),
                new(ClaimTypes.Role, RoleGroups.Visitor)
            };
            return Write(claims, expiresAt);
        }
    }
}
