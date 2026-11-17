using AudioGuide.Api.BackgroundJobs;
using AudioGuide.Api.Common;
using AudioGuide.Api.Interface.Integrations;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Models;
using AudioGuide.Api.Services;
using AudioGuide.Api.Services.Logic;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AudioGuide.Tests.Services
{
    public class ContentServiceTests
    {
        private static ContentService Service()
        {
            var languages = new Mock<ILanguageService>();
            languages.SetupGet(l => l.SourceLang).Returns("vi");
            languages.SetupGet(l => l.FallbackLang).Returns("en");
            return new ContentService(Mock.Of<IPoiRepository>(), Mock.Of<IPoiLocalizationRepository>(), Mock.Of<ITourRepository>(),
                languages.Object, TestHelpers.Mapper);
        }

        private static PoiLocalization Fresh(Poi poi, string lang, string name) => new()
        {
            PoiId = poi.Id, Lang = lang, Name = name, Description = name + " desc", AudioUrl = $"/media/{lang}.mp3",
            SourceHash = TextTools.SourceHash(poi.Name, poi.Description), Status = LocalizationStatus.Ready
        };

        [Fact]
        public void Localize_HasRequestedLanguage_ServesIt()
        {
            var poi = TestHelpers.Poi();
            var result = Service().Localize(poi, new Dictionary<string, PoiLocalization> { ["ja"] = Fresh(poi, "ja", "門") }, "ja");
            Assert.Equal("門", result.Name);
            Assert.Equal("ja", result.ServedLang);
            Assert.False(result.IsFallback);
            Assert.Equal("/media/ja.mp3", result.AudioUrl);
        }

        [Fact]
        public void Localize_MissingLanguage_FallsBackToEnglish()
        {
            var poi = TestHelpers.Poi();
            var result = Service().Localize(poi, new Dictionary<string, PoiLocalization> { ["en"] = Fresh(poi, "en", "Gate") }, "ja");
            Assert.Equal("Gate", result.Name);
            Assert.Equal("en", result.ServedLang);
            Assert.True(result.IsFallback);
        }

        [Fact]
        public void Localize_OutdatedTranslation_IgnoredAndUsesSource()
        {
            var poi = TestHelpers.Poi();
            var stale = Fresh(poi, "ja", "門");
            stale.SourceHash = "old";
            var result = Service().Localize(poi, new Dictionary<string, PoiLocalization> { ["ja"] = stale }, "ja");
            Assert.Equal(poi.Name, result.Name);
            Assert.Equal("vi", result.ServedLang);
            Assert.Null(result.AudioUrl);
        }
    }

    public class LocalizationServiceTests
    {
        private readonly Mock<IPoiRepository> _pois = new();
        private readonly Mock<IPoiLocalizationRepository> _locs = new();
        private readonly Mock<ILanguageRepository> _languages = new();
        private readonly Mock<ITranslator> _translator = new();
        private readonly Mock<ITtsProvider> _tts = new();
        private readonly Mock<IMediaStorage> _storage = new();
        private readonly Mock<IBackgroundTaskQueue> _queue = new();
        private readonly Dictionary<string, PoiLocalization> _saved = new();

        public LocalizationServiceTests()
        {
            _languages.Setup(r => r.ListAsync(true)).ReturnsAsync(new List<Language>
            {
                TestHelpers.Language("vi"), TestHelpers.Language("en"), TestHelpers.Language("zh", "zh-CN")
            });
            _languages.Setup(r => r.GetByCodeAsync("vi")).ReturnsAsync(TestHelpers.Language("vi"));
            _translator.Setup(t => t.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string text, string target, string _, CancellationToken _) => $"[{target}] {text}");
            _tts.Setup(t => t.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new byte[] { 1, 2 });
            _storage.Setup(s => s.AudioKey(It.IsAny<string>(), It.IsAny<string>())).Returns("audio/x.mp3");
            _storage.Setup(s => s.SaveAsync("audio/x.mp3", It.IsAny<byte[]>(), null)).ReturnsAsync("/media/audio/x.mp3");
            _locs.Setup(r => r.UpsertAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Action<PoiLocalization>>()))
                .ReturnsAsync((int poiId, string lang, Action<PoiLocalization> apply) =>
                {
                    if (!_saved.TryGetValue(lang, out var loc)) _saved[lang] = loc = new PoiLocalization { PoiId = poiId, Lang = lang };
                    apply(loc);
                    return loc;
                });
        }

        private LocalizationService Service() => new(_pois.Object, _locs.Object, _languages.Object, Mock.Of<ITourRepository>(),
            _translator.Object, _tts.Object, _storage.Object, _queue.Object, new MemoryCache(new MemoryCacheOptions()),
            TestHelpers.Localization(), NullLogger<LocalizationService>.Instance);

        [Fact]
        public async Task LocalizePoi_TranslatesAndMakesAudio_SourceKeptAsIs()
        {
            var poi = TestHelpers.Poi();
            _pois.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(poi);

            await Service().LocalizePoiAsync(1);

            Assert.Equal(poi.Name, _saved["vi"].Name);
            Assert.Equal(TranslatedBy.Source, _saved["vi"].TranslatedBy);
            Assert.Equal("[zh-CN] " + poi.Name, _saved["zh"].Name);
            Assert.Equal(TranslatedBy.Machine, _saved["zh"].TranslatedBy);
            Assert.Equal("/media/audio/x.mp3", _saved["zh"].AudioUrl);
            Assert.All(_saved.Values, l => Assert.Equal(LocalizationStatus.Ready, l.Status));
        }

        [Fact]
        public async Task LocalizePoi_TranslatorDown_MarksFailed_OtherLanguagesUnaffected()
        {
            _pois.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(TestHelpers.Poi());
            _translator.Setup(t => t.TranslateAsync(It.IsAny<string>(), "zh-CN", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ExternalServiceException("Google lỗi", "TRANSLATION_FAILED"));

            await Service().LocalizePoiAsync(1);

            Assert.Equal(LocalizationStatus.Failed, _saved["zh"].Status);
            Assert.Equal(LocalizationStatus.Ready, _saved["en"].Status);
        }

        [Fact]
        public async Task LocalizePoi_TtsDown_KeepsTextWithoutAudio()
        {
            _pois.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(TestHelpers.Poi());
            _tts.Setup(t => t.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ExternalServiceException("TTS lỗi", "TTS_FAILED"));

            await Service().LocalizePoiAsync(1, new[] { "en" });

            Assert.Equal(LocalizationStatus.Ready, _saved["en"].Status);
            Assert.Null(_saved["en"].AudioUrl);
            Assert.Equal("TTS lỗi", _saved["en"].Error);
        }

        [Fact]
        public async Task LocalizePoi_ManualTranslation_NotOverwritten()
        {
            var poi = TestHelpers.Poi();
            _pois.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(poi);
            _locs.Setup(r => r.GetAsync(1, "en")).ReturnsAsync(new PoiLocalization
            {
                PoiId = 1, Lang = "en", Name = "Hand made", Description = "Edited by admin", TranslatedBy = TranslatedBy.Manual,
                SourceHash = TextTools.SourceHash(poi.Name, poi.Description)
            });

            await Service().LocalizePoiAsync(1, new[] { "en" });

            Assert.Equal("Hand made", _saved["en"].Name);
            Assert.Equal(TranslatedBy.Manual, _saved["en"].TranslatedBy);
        }

        [Fact]
        public async Task SchedulePoi_UnknownLanguage_Rejected()
        {
            _pois.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(TestHelpers.Poi());
            var ex = await Assert.ThrowsAsync<BusinessException>(() => Service().SchedulePoiAsync(1, new[] { "xx" }));
            Assert.Equal("LANGUAGE_NOT_SUPPORTED", ex.Code);
        }

        [Fact]
        public async Task SchedulePoi_MarksPendingAndQueuesBackgroundJob()
        {
            _pois.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(TestHelpers.Poi());
            var queued = await Service().SchedulePoiAsync(1);
            Assert.Equal(3, queued);
            Assert.All(_saved.Values, l => Assert.Equal(LocalizationStatus.Pending, l.Status));
            _queue.Verify(q => q.Enqueue("localize:1", It.IsAny<Func<IServiceProvider, CancellationToken, Task>>()), Times.Once);
        }

        [Fact]
        public async Task DeleteOne_WhileProcessing_Conflict()
        {
            _locs.Setup(r => r.GetAsync(1, "en")).ReturnsAsync(new PoiLocalization { Status = LocalizationStatus.Processing });
            var ex = await Assert.ThrowsAsync<ConflictException>(() => Service().DeleteOneAsync(1, "en"));
            Assert.Equal("LOCALIZATION_BUSY", ex.Code);
        }
    }

    public class LanguageServiceTests
    {
        [Theory]
        [InlineData("vi")]
        [InlineData("en")]
        public async Task CannotDisable_SourceOrFallbackLanguage(string code)
        {
            var service = new LanguageService(Mock.Of<ILanguageRepository>(), TestHelpers.Mapper, TestHelpers.Localization());
            var ex = await Assert.ThrowsAsync<BusinessException>(() => service.SetActiveAsync(code, false));
            Assert.Equal("LANGUAGE_REQUIRED", ex.Code);
        }

        [Fact]
        public async Task ResolveCode_InactiveLanguage_FallsBackToEnglish()
        {
            var repo = new Mock<ILanguageRepository>();
            var ja = TestHelpers.Language("ja");
            ja.IsActive = false;
            repo.Setup(r => r.GetByCodeAsync("ja")).ReturnsAsync(ja);
            var service = new LanguageService(repo.Object, TestHelpers.Mapper, TestHelpers.Localization());
            Assert.Equal("en", await service.ResolveCodeAsync("ja"));
        }
    }
}
