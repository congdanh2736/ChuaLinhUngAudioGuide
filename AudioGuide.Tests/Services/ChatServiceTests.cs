using AudioGuide.Api.Common;
using AudioGuide.Api.Interface.Integrations;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Models;
using AudioGuide.Api.Services;
using Moq;

namespace AudioGuide.Tests.Services
{
    public class ChatServiceTests
    {
        private readonly Mock<IPoiRepository> _pois = new();
        private readonly Mock<IKnowledgeRepository> _knowledge = new();
        private readonly Mock<IChatLogRepository> _chats = new();
        private readonly Mock<IChatCacheRepository> _cache = new();
        private readonly Mock<ILanguageService> _languages = new();
        private readonly Mock<ITranslator> _translator = new();
        private readonly Mock<ILlmClient> _llm = new();

        public ChatServiceTests()
        {
            _pois.Setup(r => r.ListActiveAsync()).ReturnsAsync(new List<Poi>
            {
                TestHelpers.Poi(1, "Tượng Phật Bà Quan Thế Âm", "Tượng cao khoảng 67 mét, mặt hướng ra biển Đông."),
                TestHelpers.Poi(2, "Cổng Tam Quan", "Cổng chính có ba lối đi.")
            });
            _knowledge.Setup(r => r.ListAsync(true)).ReturnsAsync(new List<KnowledgeArticle>());
            _languages.SetupGet(l => l.SourceLang).Returns("vi");
            _languages.Setup(l => l.ResolveCodeAsync("vi")).ReturnsAsync("vi");
            _languages.Setup(l => l.GetActiveEntityAsync("vi")).ReturnsAsync(TestHelpers.Language("vi"));
            _chats.Setup(r => r.RecentForSessionAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(new List<ChatLog>());
        }

        private ChatService Service(int dailyLimit = 50) => new(_pois.Object, _knowledge.Object, _chats.Object, _cache.Object,
            _languages.Object, _translator.Object, _llm.Object, new FakeClock(new DateTime(2026, 11, 16, 2, 0, 0, DateTimeKind.Utc)),
            TestHelpers.Options(new ChatSettings { DailyLimit = dailyLimit }), TestHelpers.Options(new SiteSettings()));

        [Fact]
        public async Task WithoutAi_AnswersByQuotingRelevantDocument()
        {
            var answer = await Service().AskAsync("Tượng Quan Âm cao bao nhiêu?", "vi", 1);
            Assert.False(answer.UsedLlm);
            Assert.Contains("67 mét", answer.Answer);
            Assert.Equal("poi:1", answer.Sources[0].Id);
            _chats.Verify(r => r.AddAsync(It.IsAny<ChatLog>()), Times.Once);
        }

        [Fact]
        public async Task WithoutAi_OffTopicQuestion_PoliteRefusal()
        {
            var answer = await Service().AskAsync("Viết code python giúp mình", "vi", null);
            Assert.Equal(ChatService.OutOfScopeVi, answer.Answer);
        }

        [Fact]
        public async Task WithAi_UsesLlmAnswer()
        {
            _llm.SetupGet(l => l.IsEnabled).Returns(true);
            _llm.Setup(l => l.CompleteAsync(It.IsAny<IReadOnlyList<LlmMessage>>(), It.IsAny<double>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Tượng Phật Bà Quan Thế Âm cao khoảng 67 mét.");
            var answer = await Service().AskAsync("Tượng cao bao nhiêu?", "vi", 1);
            Assert.True(answer.UsedLlm);
            Assert.Contains(answer.Sources, s => s.Id == "poi:1");
        }

        [Fact]
        public async Task AiDown_FallsBackToExtractive()
        {
            _llm.SetupGet(l => l.IsEnabled).Returns(true);
            _llm.Setup(l => l.CompleteAsync(It.IsAny<IReadOnlyList<LlmMessage>>(), It.IsAny<double>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ExternalServiceException("quá tải", "LLM_BUSY"));
            var answer = await Service().AskAsync("Tượng Quan Âm cao bao nhiêu?", "vi", 1);
            Assert.False(answer.UsedLlm);
            Assert.Contains("67 mét", answer.Answer);
        }

        [Fact]
        public async Task DailyLimitReached_RateLimited()
        {
            _chats.Setup(r => r.CountSinceAsync(1, It.IsAny<DateTime>())).ReturnsAsync(5);
            var ex = await Assert.ThrowsAsync<RateLimitException>(() => Service(dailyLimit: 5).AskAsync("Giờ mở cửa?", "vi", 1));
            Assert.Equal("CHAT_LIMIT_REACHED", ex.Code);
        }

        [Fact]
        public void CacheKey_IgnoresAccentsAndPunctuation()
            => Assert.Equal(ChatService.CacheKey("Giờ mở cửa?", "vi"), ChatService.CacheKey("gio mo cua", "vi"));
    }
}
