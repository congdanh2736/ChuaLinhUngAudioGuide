using System.Net;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace AudioGuide.Api.Controllers
{
    /*
     * Phục vụ file audio lưu trong SQL Server.
     * enableRangeProcessing: hỗ trợ header Range (206 Partial Content) để trình duyệt tua được audio.
     * Cache lâu dài vì tên file là hash nội dung (nội dung đổi -> tên đổi).
     */
    [ApiController]
    [Route("media")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class MediaController : ControllerBase
    {
        private readonly IMediaService _service;
        public MediaController(IMediaService service) => _service = service;

        [HttpGet("{**key}")]
        public async Task<IActionResult> Get(string key)
        {
            var (data, contentType) = await _service.GetAsync(key);
            Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            var etag = new EntityTagHeaderValue($"\"{Path.GetFileNameWithoutExtension(key)}\"");
            return File(data, contentType, lastModified: null, entityTag: etag, enableRangeProcessing: true);
        }
    }

    /*
     * Trang CỔNG THANH TOÁN GIẢ LẬP (thay Payoo khi demo).
     * Trang này đóng vai "bên thứ 3": khách bấm Thanh toán -> cổng tự ký rồi gọi xử lý webhook như cổng thật
     * -> chuyển khách về app kèm mã truy cập.
     */
    [ApiController]
    [Route("mock-gateway")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class MockGatewayController : ControllerBase
    {
        private readonly IPaymentService _service;
        private readonly string _frontendUrl;   // frontend deploy riêng (Vercel) -> chuyển khách về đó, trống = về chính backend

        public MockGatewayController(IPaymentService service, IOptions<SiteSettings> site)
        {
            _service = service;
            _frontendUrl = site.Value.FrontendUrl.Trim().TrimEnd('/');
        }

        private const string Page = """
<!doctype html><html lang="vi"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1"><title>Cổng thanh toán (giả lập)</title>
<link rel="stylesheet" href="/assets/css/base.css"><link rel="stylesheet" href="/assets/css/components.css">
<style>body{padding:16px}.gw{max-width:380px;margin:32px auto}.amount{font-size:28px;font-weight:700;margin:12px 0}
.gw form{display:grid;gap:8px;margin-top:12px}</style></head><body><div class="card gw">
<div class="alert alert-info">Môi trường giả lập - không trừ tiền thật</div>
<h2>Thanh toán vé thuyết minh</h2><div class="text-muted">Mã giao dịch: {id}</div>
<div class="amount">{amount}</div>{body}</div></body></html>
""";

        private ContentResult Html(string id, string amount, string body, int status = 200)
            => new()
            {
                Content = Page.Replace("{id}", WebUtility.HtmlEncode(id)).Replace("{amount}", amount).Replace("{body}", body),
                ContentType = "text/html; charset=utf-8",
                StatusCode = status
            };

        [HttpGet("{paymentId:int}")]
        public async Task<IActionResult> PaymentPage(int paymentId)
        {
            try
            {
                var (payment, _) = await _service.GetPaymentAsync(paymentId);
                var amount = payment.Amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", ".") + " đ";
                var body = payment.Status != Models.PaymentStatus.Pending
                    ? $"<p>Giao dịch đã xử lý: <b>{payment.Status}</b></p><a class=\"btn btn-secondary\" href=\"{_frontendUrl}/\">Về ứng dụng</a>"
                    : $"<form method=\"post\" action=\"/mock-gateway/{payment.Id}/complete\">"
                      + "<button class=\"btn btn-primary\" name=\"result\" value=\"success\">Thanh toán thành công</button>"
                      + "<button class=\"btn btn-secondary\" name=\"result\" value=\"failed\">Hủy / thất bại</button></form>";
                return Html(payment.Id.ToString(), amount, body);
            }
            catch (Common.BusinessException ex)
            {
                return Html(paymentId.ToString(), "—", $"<p>{WebUtility.HtmlEncode(ex.Message)}</p>", 404);
            }
        }

        [HttpPost("{paymentId:int}/complete")]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> Complete(int paymentId, [FromForm] string result)
        {
            var payment = await _service.CompleteMockPaymentAsync(paymentId, result == "success");
            var (_, session) = await _service.GetPaymentAsync(payment.Id);
            return payment.Status == Models.PaymentStatus.Success
                ? Redirect($"{_frontendUrl}/?payment=success&code={session.Code}")
                : Redirect($"{_frontendUrl}/?payment=failed");
        }
    }

    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {
        private readonly Data.ApplicationDbContext _db;
        public HealthController(Data.ApplicationDbContext db) => _db = db;

        // Server còn sống
        [HttpGet]
        public IActionResult Live() => Ok(new { status = "ok" });

        // Kết nối database OK
        [HttpGet("ready")]
        public async Task<IActionResult> Ready()
        {
            var ok = await _db.Database.CanConnectAsync();
            return StatusCode(ok ? 200 : 503, new { status = ok ? "ready" : "not_ready", database = ok });
        }
    }
}
