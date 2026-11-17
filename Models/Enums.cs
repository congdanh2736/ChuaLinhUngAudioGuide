namespace AudioGuide.Api.Models
{
    /*
     * Các trạng thái / loại dữ liệu dùng chung.
     * Lưu xuống SQL Server dạng CHỮ (vd "Ready", "Cash") cho dễ đọc khi mở bảng trong SSMS
     * (cấu hình trong ApplicationDbContext: HasConversion<string>()).
     */

    // Trạng thái bản dịch + audio của 1 điểm tham quan ở 1 ngôn ngữ
    public enum LocalizationStatus
    {
        Pending,     // đã xếp hàng, chưa xử lý
        Processing,  // đang dịch / tạo audio
        Ready,       // đã có bản dịch (audio có thể có hoặc không)
        Failed,      // dịch lỗi
        Outdated     // nội dung gốc đã đổi, bản dịch cũ không còn đúng
    }

    // Bản dịch do đâu mà có
    public enum TranslatedBy
    {
        Source,   // chính là nội dung gốc tiếng Việt
        Machine,  // dịch máy
        Manual    // admin sửa tay (không bị dịch máy ghi đè)
    }

    // Hình thức mua quyền truy cập
    public enum AccessMethod
    {
        Cash,    // trả tiền mặt tại quầy, nhân viên tạo mã
        Online   // thanh toán qua cổng (giả lập)
    }

    /*
     * Vòng đời 1 phiên truy cập:
     *   Online: Pending --(cổng báo thành công)--> Paid --(khách đổi mã)--> Active --> Expired
     *   Cash  : (nhân viên đã thu tiền) Paid --(khách đổi mã)--> Active --> Expired
     *   Bất kỳ: --(thanh toán lỗi / admin thu hồi)--> Cancelled
     */
    public enum AccessStatus
    {
        Pending,
        Paid,
        Active,
        Expired,
        Cancelled
    }

    public enum PaymentStatus
    {
        Pending,
        Success,
        Failed
    }

    // Hành vi của du khách được ghi nhận để thống kê
    public enum EventType
    {
        View,           // mở chi tiết điểm tham quan
        AudioPlay,      // nghe audio
        GeofenceEnter   // GPS phát hiện đi vào vùng của điểm
    }
}
