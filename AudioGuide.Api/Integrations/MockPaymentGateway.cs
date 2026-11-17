using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AudioGuide.Api.Common;
using AudioGuide.Api.Interface.Integrations;
using Microsoft.Extensions.Options;

namespace AudioGuide.Api.Integrations
{
    /*
     * Cổng thanh toán GIẢ LẬP (thay Payoo/VNPay vì nhóm không có tài khoản merchant), mô phỏng đúng quy trình thật:
     *   1. Backend tạo URL thanh toán -> 2. Khách thanh toán trên trang của cổng
     *   3. Cổng gọi webhook (có chữ ký HMAC-SHA256) báo kết quả -> 4. Cổng chuyển khách về app.
     * Có cổng thật thì chỉ cần viết class mới cùng interface IPaymentGateway.
     */
    public class MockPaymentGateway : IPaymentGateway
    {
        private readonly string _secret;
        public MockPaymentGateway(IOptions<PaymentSettings> options) => _secret = options.Value.WebhookSecret;

        public string Name => "mock";

        // Cổng giả lập chạy cùng server -> dùng URL tương đối, deploy ở domain nào cũng đúng
        public string CreatePaymentUrl(int paymentId, int amount) => $"/mock-gateway/{paymentId}";

        // Chuỗi JSON chuẩn hóa (sắp xếp key) để 2 bên ký cùng 1 nội dung
        public static string CanonicalJson(IDictionary<string, string?> payload)
            => JsonSerializer.Serialize(new SortedDictionary<string, string?>(payload, StringComparer.Ordinal));

        public string Sign(IDictionary<string, string?> payload)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secret));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(CanonicalJson(payload)))).ToLowerInvariant();
        }

        // So sánh chữ ký theo thời gian cố định (chống đoán chữ ký từng ký tự)
        public bool VerifyWebhook(IDictionary<string, string?> payload, string signature)
            => CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(Sign(payload)), Encoding.UTF8.GetBytes(signature ?? ""));
    }
}
