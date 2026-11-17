using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Auth;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Models;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AudioGuide.Api.Services
{
    /*
     * Nghiệp vụ tài khoản quản trị: đăng nhập, đổi mật khẩu, quản lý nhân viên.
     * Dùng UserManager của ASP.NET Core Identity (băm mật khẩu, kiểm tra mật khẩu, gán vai trò).
     */
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IJwtService _jwtService;
        private readonly IMapper _mapper;

        public AuthService(UserManager<ApplicationUser> userManager, IJwtService jwtService, IMapper mapper)
        {
            _userManager = userManager;
            _jwtService = jwtService;
            _mapper = mapper;
        }

        private async Task<UserDto> ToDtoAsync(ApplicationUser user)
        {
            var dto = _mapper.Map<UserDto>(user);
            var roles = await _userManager.GetRolesAsync(user);
            dto.Role = roles.Contains(RoleGroups.Admin) ? RoleGroups.Admin : nameof(AppRole.Staff);
            return dto;
        }

        private static string IdentityErrors(IdentityResult result)
            => string.Join(" ", result.Errors.Select(e => e.Description));

        public async Task<LoginResultDto> LoginAsync(LoginDto dto)
        {
            var user = await _userManager.FindByNameAsync(dto.UserName.Trim());
            // Cùng 1 thông báo cho "sai tài khoản" và "sai mật khẩu" để không lộ tài khoản nào tồn tại
            if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
                throw new UnauthorizedException("Sai tên đăng nhập hoặc mật khẩu", "INVALID_CREDENTIALS");
            if (!user.IsActive)
                throw new UnauthorizedException("Tài khoản đã bị khóa", "ACCOUNT_DISABLED");

            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            var (token, expiresAt) = await _jwtService.CreateAdminTokenAsync(user);
            return new LoginResultDto { Token = token, ExpiresAt = expiresAt, User = await ToDtoAsync(user) };
        }

        public async Task<UserDto> GetUserAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || !user.IsActive) throw new UnauthorizedException("Tài khoản không còn hiệu lực", "ACCOUNT_DISABLED");
            return await ToDtoAsync(user);
        }

        public async Task ChangePasswordAsync(string userId, ChangePasswordDto dto)
        {
            var user = await _userManager.FindByIdAsync(userId)
                       ?? throw new UnauthorizedException("Tài khoản không còn hiệu lực", "ACCOUNT_DISABLED");
            if (!await _userManager.CheckPasswordAsync(user, dto.OldPassword))
                throw new BusinessException("Mật khẩu hiện tại không đúng", "WRONG_PASSWORD");
            var result = await _userManager.ChangePasswordAsync(user, dto.OldPassword, dto.NewPassword);
            if (!result.Succeeded) throw new BusinessException(IdentityErrors(result), "INVALID_PASSWORD");
        }

        public async Task<List<UserDto>> ListUsersAsync()
        {
            var users = await _userManager.Users.OrderBy(u => u.UserName).ToListAsync();
            var result = new List<UserDto>();
            foreach (var user in users) result.Add(await ToDtoAsync(user));
            return result;
        }

        public async Task<UserDto> CreateUserAsync(CreateUserDto dto)
        {
            var userName = dto.UserName.Trim().ToLowerInvariant();
            if (await _userManager.FindByNameAsync(userName) != null)
                throw new ConflictException("Tên đăng nhập đã tồn tại", "USERNAME_EXISTS");
            var user = new ApplicationUser
            {
                UserName = userName,
                FullName = dto.FullName.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var created = await _userManager.CreateAsync(user, dto.Password);
            if (!created.Succeeded) throw new BusinessException(IdentityErrors(created), "INVALID_USER");
            var role = Enum.Parse<AppRole>(dto.Role, ignoreCase: true).ToString();
            await _userManager.AddToRoleAsync(user, role);
            return await ToDtoAsync(user);
        }

        public async Task<UserDto> UpdateUserAsync(string id, UpdateUserDto dto, string actorId)
        {
            var user = await _userManager.FindByIdAsync(id) ?? throw new NotFoundException("Không tìm thấy tài khoản", "USER_NOT_FOUND");
            var newRole = dto.Role == null ? null : Enum.Parse<AppRole>(dto.Role, ignoreCase: true).ToString();
            // Không cho admin tự khóa hoặc tự hạ quyền -> tránh hệ thống không còn ai quản trị
            if (user.Id == actorId && (dto.IsActive == false || newRole == nameof(AppRole.Staff)))
                throw new BusinessException("Không thể tự khóa hoặc tự hạ quyền chính mình", "SELF_LOCKOUT");

            if (dto.FullName != null) user.FullName = dto.FullName.Trim();
            if (dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;
            await _userManager.UpdateAsync(user);

            if (newRole != null)
            {
                var current = await _userManager.GetRolesAsync(user);
                if (!current.Contains(newRole))
                {
                    await _userManager.RemoveFromRolesAsync(user, current);
                    await _userManager.AddToRoleAsync(user, newRole);
                }
            }
            if (!string.IsNullOrEmpty(dto.Password))
            {
                // Admin đặt lại mật khẩu cho nhân viên quên mật khẩu
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var reset = await _userManager.ResetPasswordAsync(user, resetToken, dto.Password);
                if (!reset.Succeeded) throw new BusinessException(IdentityErrors(reset), "INVALID_PASSWORD");
            }
            return await ToDtoAsync(user);
        }
    }
}
