using System.Globalization;
using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Access;
using AudioGuide.Api.Interface.Integrations;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Models;

namespace AudioGuide.Api.Services
{
    // Nghiệp vụ thanh toán online
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IAccessSessionRepository _sessionRepository;
        private readonly IAccessService _accessService;
        private readonly IPaymentGateway _gateway;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(IPaymentRepository paymentRepository, IAccessSessionRepository sessionRepository,
            IAccessService accessService, IPaymentGateway gateway, ILogger<PaymentService> logger)
        {
            _paymentRepository = paymentRepository;
            _sessionRepository = sessionRepository;
            _accessService = accessService;
            _gateway = gateway;
            _logger = logger;
        }

        // B1: tạo phiên chờ thanh toán + bản ghi Payment + URL cổng thanh toán
        public async Task<OnlinePaymentDto> StartOnlinePaymentAsync()
        {
            var session = await _accessService.CreateSessionAsync(AccessMethod.Online, AccessStatus.Pending);
            var payment = new Payment
            {
                SessionId = session.Id,
                Provider = _gateway.Name,
                Amount = session.Amount,
                Currency = session.Currency,
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            await _paymentRepository.AddAsync(payment);
            return new OnlinePaymentDto
            {
                PaymentId = payment.Id,
                PaymentUrl = _gateway.CreatePaymentUrl(payment.Id, payment.Amount),
                Amount = payment.Amount,
                Currency = payment.Currency
            };
        }

        public async Task<(Payment Payment, AccessSession Session)> GetPaymentAsync(int paymentId)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId)
                          ?? throw new NotFoundException("Không tìm thấy giao dịch", "PAYMENT_NOT_FOUND");
            var session = await _sessionRepository.GetByIdAsync(payment.SessionId)
                          ?? throw new NotFoundException("Không tìm thấy phiên truy cập", "SESSION_NOT_FOUND");
            return (payment, session);
        }

        public static Dictionary<string, string?> ToPayload(WebhookRequestDto dto) => new()
        {
            ["payment_id"] = dto.PaymentId.ToString(CultureInfo.InvariantCulture),
            ["status"] = dto.Status,
            ["provider_ref"] = dto.ProviderRef
        };

        /*
         * B2: cổng thanh toán báo kết quả.
         * - Chữ ký sai -> từ chối (chống giả mạo "đã thanh toán").
         * - Idempotent: cổng có thể gọi lại nhiều lần, chỉ xử lý lần đầu.
         */
        public async Task<Payment> HandleWebhookAsync(WebhookRequestDto dto, string signature)
        {
            if (!_gateway.VerifyWebhook(ToPayload(dto), signature))
                throw new UnauthorizedException("Chữ ký webhook không hợp lệ", "INVALID_SIGNATURE");
            var (payment, session) = await GetPaymentAsync(dto.PaymentId);
            if (payment.Status != PaymentStatus.Pending) return payment;

            var now = DateTime.UtcNow;
            var success = dto.Status == "success";
            payment.Status = success ? PaymentStatus.Success : PaymentStatus.Failed;
            payment.ProviderRef = dto.ProviderRef;
            payment.CompletedAt = now;
            await _paymentRepository.UpdateAsync(payment);

            if (success)
            {
                session.Status = AccessStatus.Paid;
                session.PaidAt = now;
            }
            else
            {
                session.Status = AccessStatus.Cancelled;
            }
            await _sessionRepository.UpdateAsync(session);
            _logger.LogInformation("Thanh toán {PaymentId} -> {Status}", payment.Id, payment.Status);
            return payment;
        }

        // Trang cổng giả lập: khách bấm "Thanh toán thành công / thất bại" -> cổng tự ký rồi gọi webhook như cổng thật
        public async Task<Payment> CompleteMockPaymentAsync(int paymentId, bool success)
        {
            var dto = new WebhookRequestDto
            {
                PaymentId = paymentId,
                Status = success ? "success" : "failed",
                ProviderRef = "MOCK-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()
            };
            return await HandleWebhookAsync(dto, _gateway.Sign(ToPayload(dto)));
        }
    }
}
