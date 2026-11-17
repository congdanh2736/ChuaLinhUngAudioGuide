namespace AudioGuide.Api.Models
{
    // Một lượt mua quyền truy cập. Code là mã ngắn (6 ký tự) du khách nhập vào app.
    public class AccessSession
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public AccessMethod Method { get; set; }
        public AccessStatus Status { get; set; }
        public int Amount { get; set; }
        public string Currency { get; set; } = "VND";
        public string? CreatedById { get; set; }          // nhân viên bán mã (tiền mặt); null nếu mua online
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime CodeExpiresAt { get; set; }       // hạn để đổi mã
        public DateTime? PaidAt { get; set; }
        public DateTime? ActivatedAt { get; set; }
        public DateTime? AccessExpiresAt { get; set; }    // hạn sử dụng app sau khi kích hoạt

        public ApplicationUser? CreatedBy { get; set; }
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public ICollection<ChatLog> ChatLogs { get; set; } = new List<ChatLog>();
        public ICollection<VisitEvent> VisitEvents { get; set; } = new List<VisitEvent>();
    }

    // Giao dịch thanh toán online của 1 phiên truy cập
    public class Payment
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public string Provider { get; set; } = "mock";
        public int Amount { get; set; }
        public string Currency { get; set; } = "VND";
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public string? ProviderRef { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public AccessSession? Session { get; set; }
    }
}
