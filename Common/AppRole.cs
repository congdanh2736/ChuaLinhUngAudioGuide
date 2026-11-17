namespace AudioGuide.Api.Common
{
    // Vai trò tài khoản quản trị (lưu trong bảng AspNetRoles của Identity)
    public enum AppRole
    {
        Admin,  // toàn quyền
        Staff   // nhân viên quầy vé: tạo mã truy cập tiền mặt, xem dữ liệu
    }

    // Nhóm vai trò dùng trong [Authorize(Roles = ...)]
    public static class RoleGroups
    {
        public const string Admin = nameof(AppRole.Admin);
        public const string Staff = nameof(AppRole.Admin) + "," + nameof(AppRole.Staff); // admin + nhân viên
        public const string Visitor = "Visitor"; // token du khách (sau khi đổi mã truy cập)
    }
}
