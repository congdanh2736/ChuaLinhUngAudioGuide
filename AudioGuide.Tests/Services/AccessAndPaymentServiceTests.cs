using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Access;
using AudioGuide.Api.Integrations;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Models;
using AudioGuide.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AudioGuide.Tests.Services
{
    // Test lớp Service bằng Repository GIẢ (Moq) -> không cần database
    public class AccessServiceTests
    {
        private readonly Mock<IAccessSessionRepository> _repo = new();
        private readonly Mock<IJwtService> _jwt = new();
        private readonly FakeClock _clock = new(new DateTime(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc));

        private AccessService Service() => new(_repo.Object, _jwt.Object, TestHelpers.Mapper,
            TestHelpers.Options(new AccessSettings { AccessHours = 24, CodeTtlHours = 24, PriceVnd = 50000 }), _clock);

        // Chọn giảm giá -> số tiền lưu vào phiên truy cập là giá đã giảm
        [Theory]
        [InlineData(0, 50000)]
        [InlineData(5, 47500)]
        [InlineData(10, 45000)]
        public async Task CreateSession_AppliesDiscount(int percent, int expectedAmount)
        {
            AccessSession? saved = null;
            _repo.Setup(r => r.AddAsync(It.IsAny<AccessSession>())).Callback<AccessSession>(s => saved = s).Returns(Task.CompletedTask);
            await Service().CreateSessionAsync(AccessMethod.Online, AccessStatus.Pending, discountPercent: percent);
            Assert.Equal(expectedAmount, saved!.Amount);
        }

        [Fact]
        public async Task CreateSession_InvalidDiscount_Throws()
        {
            var ex = await Assert.ThrowsAsync<BusinessException>(() =>
                Service().CreateSessionAsync(AccessMethod.Online, AccessStatus.Pending, discountPercent: 90));
            Assert.Equal("INVALID_DISCOUNT", ex.Code);
        }

        private AccessSession Session(AccessStatus status) => new()
        {
            Id = 7, Code = "AB3K9Q", Status = status, Method = AccessMethod.Cash, Amount = 50000,
            CreatedAt = _clock.GetUtcNow().UtcDateTime, CodeExpiresAt = _clock.GetUtcNow().UtcDateTime.AddHours(24)
        };

        [Fact]
        public async Task Redeem_PaidCode_ActivatesAndIssuesToken()
        {
            var session = Session(AccessStatus.Paid);
            _repo.Setup(r => r.GetByCodeAsync("AB3K9Q")).ReturnsAsync(session);
            _jwt.Setup(j => j.CreateVisitorToken(7, It.IsAny<DateTime>())).Returns("token");

            var result = await Service().RedeemAsync(" ab3k9q ");

            Assert.Equal("token", result.Token);
            Assert.Equal(AccessStatus.Active, session.Status);
            Assert.Equal(_clock.GetUtcNow().UtcDateTime.AddHours(24), session.AccessExpiresAt);
            _repo.Verify(r => r.UpdateAsync(session), Times.Once);
        }

        [Fact]
        public async Task Redeem_PendingPayment_ThrowsPaymentPending()
        {
            _repo.Setup(r => r.GetByCodeAsync("AB3K9Q")).ReturnsAsync(Session(AccessStatus.Pending));
            var ex = await Assert.ThrowsAsync<PaymentPendingException>(() => Service().RedeemAsync("AB3K9Q"));
            Assert.Equal("PAYMENT_PENDING", ex.Code);
        }

        [Fact]
        public async Task Redeem_ExpiredCode_MarksExpired()
        {
            var session = Session(AccessStatus.Paid);
            _repo.Setup(r => r.GetByCodeAsync("AB3K9Q")).ReturnsAsync(session);
            _clock.Advance(TimeSpan.FromHours(25));
            var ex = await Assert.ThrowsAsync<BusinessException>(() => Service().RedeemAsync("AB3K9Q"));
            Assert.Equal("ACCESS_EXPIRED", ex.Code);
            Assert.Equal(AccessStatus.Expired, session.Status);
        }

        [Fact]
        public async Task Redeem_UnknownCode_NotFound()
        {
            var ex = await Assert.ThrowsAsync<NotFoundException>(() => Service().RedeemAsync("ZZZZZZ"));
            Assert.Equal("INVALID_ACCESS_CODE", ex.Code);
        }

        [Fact]
        public async Task VerifySession_RevokedSession_Unauthorized()
        {
            _repo.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(Session(AccessStatus.Cancelled));
            var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => Service().VerifySessionAsync(7));
            Assert.Equal("ACCESS_REVOKED", ex.Code);
        }

        [Fact]
        public async Task CreateCashCodes_CreatesPaidSessionsWithUniqueCodes()
        {
            var created = new List<AccessSession>();
            _repo.Setup(r => r.AddAsync(It.IsAny<AccessSession>())).Callback<AccessSession>(created.Add).Returns(Task.CompletedTask);

            var result = await Service().CreateCashCodesAsync("staff-1", 3, "Đoàn khách");

            Assert.Equal(3, result.Count);
            Assert.All(created, s => Assert.Equal(AccessStatus.Paid, s.Status));
            Assert.All(created, s => Assert.Equal(6, s.Code.Length));
            Assert.Equal(3, created.Select(s => s.Code).Distinct().Count());
        }

        [Fact]
        public async Task CreateCashCodes_TooMany_Rejected()
            => await Assert.ThrowsAsync<BusinessException>(() => Service().CreateCashCodesAsync("staff-1", 51, null));
    }

    public class PaymentServiceTests
    {
        private readonly Mock<IPaymentRepository> _payments = new();
        private readonly Mock<IAccessSessionRepository> _sessions = new();
        private readonly Mock<IAccessService> _access = new();
        private readonly MockPaymentGateway _gateway = new(TestHelpers.Options(new PaymentSettings { WebhookSecret = "secret" }));

        private PaymentService Service() => new(_payments.Object, _sessions.Object, _access.Object, _gateway,
            NullLogger<PaymentService>.Instance);

        [Fact]
        public async Task Webhook_InvalidSignature_Rejected()
        {
            var dto = new WebhookRequestDto { PaymentId = 1, Status = "success" };
            var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => Service().HandleWebhookAsync(dto, "bad"));
            Assert.Equal("INVALID_SIGNATURE", ex.Code);
        }

        [Fact]
        public async Task MockPaymentSuccess_MarksSessionPaid_AndIsIdempotent()
        {
            var payment = new Payment { Id = 1, SessionId = 9, Status = PaymentStatus.Pending, Amount = 50000 };
            var session = new AccessSession { Id = 9, Status = AccessStatus.Pending };
            _payments.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(payment);
            _sessions.Setup(r => r.GetByIdAsync(9)).ReturnsAsync(session);

            await Service().CompleteMockPaymentAsync(1, success: true);
            Assert.Equal(PaymentStatus.Success, payment.Status);
            Assert.Equal(AccessStatus.Paid, session.Status);

            // Cổng gọi lại lần 2 -> không xử lý lại
            await Service().CompleteMockPaymentAsync(1, success: false);
            Assert.Equal(PaymentStatus.Success, payment.Status);
            _sessions.Verify(r => r.UpdateAsync(session), Times.Once);
        }
    }
}
