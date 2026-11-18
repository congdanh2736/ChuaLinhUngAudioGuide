using System.Security.Claims;
using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Access;
using AudioGuide.Api.DTOs.Common;
using AudioGuide.Api.Filters;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AudioGuide.Api.Controllers
{
    // Mua quyền truy cập (du khách) + quản lý mã truy cập (nhân viên)
    [ApiController]
    [Route("api/access")]
    public class AccessController : ControllerBase
    {
        private readonly IAccessService _accessService;
        private readonly IPaymentService _paymentService;

        public AccessController(IAccessService accessService, IPaymentService paymentService)
        {
            _accessService = accessService;
            _paymentService = paymentService;
        }

        // ------------------------------------------------ du khách
        // Giá vé, thời gian dùng
        [HttpGet("info")]
        public ActionResult<AccessInfoDto> Info() => Ok(_accessService.GetInfo());

        // Bắt đầu thanh toán online -> nhận URL cổng thanh toán
        [HttpPost("online")]
        public async Task<ActionResult<OnlinePaymentDto>> StartOnline([FromBody] StartOnlineRequestDto? dto)
            => StatusCode(StatusCodes.Status201Created, await _paymentService.StartOnlinePaymentAsync(dto?.DiscountPercent ?? 0));

        // Đổi mã truy cập (online hoặc tiền mặt) lấy token
        [HttpPost("token")]
        public async Task<ActionResult<AccessTokenDto>> Redeem([FromBody] RedeemRequestDto dto)
            => Ok(await _accessService.RedeemAsync(dto.Code));

        // Quyền truy cập hiện tại
        [VisitorAccess]
        [HttpGet("me")]
        public ActionResult<VisitorSessionDto> Me()
        {
            var visitor = HttpContext.GetVisitor();
            return Ok(new VisitorSessionDto
            {
                SessionId = visitor.SessionId,
                Status = visitor.Status,
                AccessExpiresAt = visitor.AccessExpiresAt,
                IsAdminPreview = visitor.IsAdminPreview
            });
        }

        // ------------------------------------------------ nhân viên
        // Đã thu tiền mặt -> tạo mã cho khách
        [Authorize(Roles = RoleGroups.Staff)]
        [HttpPost("cash-codes")]
        public async Task<ActionResult<List<AccessSessionDto>>> CreateCashCodes([FromBody] CashCodeRequestDto dto)
        {
            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return StatusCode(StatusCodes.Status201Created, await _accessService.CreateCashCodesAsync(staffId, dto.Quantity, dto.Note));
        }

        [Authorize(Roles = RoleGroups.Staff)]
        [HttpGet("sessions")]
        public async Task<ActionResult<PagedResult<AccessSessionDto>>> Sessions([FromQuery] AccessStatus? status,
            [FromQuery] int page = 1, [FromQuery] int size = 20)
            => Ok(await _accessService.SearchAsync(status, page, size));

        // Thu hồi quyền truy cập (token du khách hết hiệu lực ngay lần gọi API kế tiếp)
        [Authorize(Roles = RoleGroups.Admin)]
        [HttpPost("sessions/{id:int}/revoke")]
        public async Task<ActionResult<AccessSessionDto>> Revoke(int id) => Ok(await _accessService.RevokeAsync(id));
    }

    // Cổng thanh toán gọi về báo kết quả
    [ApiController]
    [Route("api/payments")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _service;
        public PaymentController(IPaymentService service) => _service = service;

        [HttpPost("webhook")]
        public async Task<ActionResult<WebhookResultDto>> Webhook([FromBody] WebhookRequestDto dto, [FromHeader(Name = "X-Signature")] string? signature)
        {
            var payment = await _service.HandleWebhookAsync(dto, signature ?? "");
            return Ok(new WebhookResultDto { PaymentId = payment.Id, Status = payment.Status.ToString() });
        }
    }
}
