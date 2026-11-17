namespace AudioGuide.Api.DTOs.Auth
{
    // Thông tin tài khoản trả về client - KHÔNG BAO GIỜ trả mật khẩu đã băm
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;   // Admin | Staff
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    public class CreateUserDto
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = "Staff";
    }

    // Chỉ gửi trường muốn sửa (null = giữ nguyên)
    public class UpdateUserDto
    {
        public string? FullName { get; set; }
        public string? Role { get; set; }
        public bool? IsActive { get; set; }
        public string? Password { get; set; }   // admin đặt lại mật khẩu cho nhân viên
    }
}
