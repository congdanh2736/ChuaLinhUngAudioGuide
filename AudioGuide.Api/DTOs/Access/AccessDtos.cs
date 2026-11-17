namespace AudioGuide.Api.DTOs.Access
{
    public class AccessInfoDto
    {
        public int PriceVnd { get; set; }
        public int AccessHours { get; set; }
        public List<string> Methods { get; set; } = new() { "Online", "Cash" };
        public bool AccessRequired { get; set; }
    }

    // Du khách nhập mã truy cập (6 ký tự) để nhận token
    public class RedeemRequestDto
    {
        public string Code { get; set; } = string.Empty;
    }

    public class AccessTokenDto
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    public class OnlinePaymentDto
    {
        public int PaymentId { get; set; }
        public string PaymentUrl { get; set; } = string.Empty;
        public int Amount { get; set; }
        public string Currency { get; set; } = "VND";
    }

    // Quyền truy cập hiện tại của người đang gọi API
    public class VisitorSessionDto
    {
        public int? SessionId { get; set; }
        public string? Status { get; set; }
        public DateTime? AccessExpiresAt { get; set; }
        public bool IsAdminPreview { get; set; }   // admin đăng nhập xem thử app
    }

    // Nhân viên đã thu tiền mặt -> tạo mã cho khách
    public class CashCodeRequestDto
    {
        public int Quantity { get; set; } = 1;
        public string? Note { get; set; }
    }

    public class AccessSessionDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Amount { get; set; }
        public string Currency { get; set; } = "VND";
        public string? Note { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime CodeExpiresAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? ActivatedAt { get; set; }
        public DateTime? AccessExpiresAt { get; set; }
    }

    // Cổng thanh toán gọi về báo kết quả (kèm chữ ký HMAC ở header X-Signature)
    public class WebhookRequestDto
    {
        public int PaymentId { get; set; }
        public string Status { get; set; } = string.Empty;   // success | failed
        public string? ProviderRef { get; set; }
    }

    public class WebhookResultDto
    {
        public int PaymentId { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
