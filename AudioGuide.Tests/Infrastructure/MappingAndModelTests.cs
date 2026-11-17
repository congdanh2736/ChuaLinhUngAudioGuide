using AudioGuide.Api.Data;
using AudioGuide.Api.Integrations;
using AudioGuide.Api.Common;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AudioGuide.Tests.Infrastructure
{
    public class MappingAndModelTests
    {
        // Mọi trường của DTO đều có nguồn dữ liệu (quên map 1 trường -> test đỏ)
        [Fact]
        public void AutoMapperConfiguration_IsValid()
        {
            var config = new MapperConfiguration(cfg => cfg.AddMaps(typeof(AudioGuide.Api.Services.PoiService).Assembly),
                NullLoggerFactory.Instance);
            config.AssertConfigurationIsValid();
        }

        private static ApplicationDbContext NewContext()
            => new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=AudioGuideTest;Trusted_Connection=True;").Options);

        // Migration đã khớp với Models + ApplicationDbContext (sửa Model mà quên Add-Migration -> test đỏ)
        [Fact]
        public void Migrations_AreUpToDateWithModel()
        {
            using var db = NewContext();
            Assert.False(db.Database.HasPendingModelChanges());
        }

        // Khóa ngoại: chỉ bảng con "thuộc sở hữu" mới được CASCADE, còn lại NO ACTION
        [Fact]
        public void ForeignKeys_AreNoAction_ExceptOwnedChildren()
        {
            using var db = NewContext();
            var cascade = db.Model.GetEntityTypes()
                .Where(e => !e.ClrType.Namespace!.StartsWith("Microsoft.AspNetCore.Identity") && e.ClrType.Name != "ApplicationUser")
                .SelectMany(e => e.GetForeignKeys())
                .Where(fk => fk.DeleteBehavior == DeleteBehavior.Cascade)
                .Select(fk => $"{fk.DeclaringEntityType.ClrType.Name}->{fk.PrincipalEntityType.ClrType.Name}")
                .OrderBy(x => x)
                .ToList();
            Assert.Equal(new[] { "PoiOpeningHour->Poi", "TourPoi->Tour", "TourTranslation->Tour" }, cascade);
        }

        [Fact]
        public void MockGateway_SignatureRoundTrip()
        {
            var gateway = new MockPaymentGateway(TestHelpers.Options(new PaymentSettings { WebhookSecret = "s3cret" }));
            var payload = new Dictionary<string, string?> { ["payment_id"] = "5", ["status"] = "success", ["provider_ref"] = "X" };
            var signature = gateway.Sign(payload);
            Assert.True(gateway.VerifyWebhook(payload, signature));
            payload["status"] = "failed";
            Assert.False(gateway.VerifyWebhook(payload, signature)); // đổi nội dung -> chữ ký sai
        }

        [Fact]
        public void EdgeTts_LongVoiceName()
            => Assert.Equal("Microsoft Server Speech Text to Speech Voice (vi-VN, HoaiMyNeural)", EdgeTtsClient.LongVoiceName("vi-VN-HoaiMyNeural"));

        [Fact]
        public void SplitText_KeepsChunksUnderLimit()
        {
            var text = string.Join(". ", Enumerable.Repeat("Câu thuyết minh khá dài về ngôi chùa", 200));
            var chunks = TranslatorText.SplitText(text, 500);
            Assert.All(chunks, c => Assert.True(c.Length <= 500));
            Assert.Equal(text, string.Concat(chunks));
        }

        [Theory]
        [InlineData("sk-or-v1-abc", "openrouter")]
        [InlineData("gsk_abc", "groq")]
        [InlineData("AIzaSyabc", "gemini")]
        public void Llm_DetectProvider(string key, string expected)
            => Assert.Equal(expected, OpenAiCompatibleLlmClient.DetectProvider(key));

        [Fact]
        public void Llm_RankModels_PrefersSameFamilyStableNewer()
        {
            var ranked = OpenAiCompatibleLlmClient.RankModels(
                new[] { "gemini-2.0-pro", "gemini-2.5-flash", "gemini-3.0-flash-preview", "text-embedding-004", "gemini-3.0-flash" },
                "gemini-1.5-flash");
            Assert.Equal("gemini-3.0-flash", ranked[0]);
            Assert.DoesNotContain("text-embedding-004", ranked);
        }
    }
}
