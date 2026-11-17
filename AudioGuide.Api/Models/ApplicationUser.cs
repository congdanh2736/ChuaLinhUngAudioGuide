using Microsoft.AspNetCore.Identity;

namespace AudioGuide.Api.Models
{
    /*
     * Tài khoản quản trị (admin / nhân viên quầy vé), dùng ASP.NET Core Identity.
     * Identity lo phần băm mật khẩu, tên đăng nhập duy nhất, vai trò (bảng AspNetUserRoles).
     */
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;   // false = bị khóa, không đăng nhập được
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }

        public ICollection<AccessSession> CreatedSessions { get; set; } = new List<AccessSession>();
    }
}
