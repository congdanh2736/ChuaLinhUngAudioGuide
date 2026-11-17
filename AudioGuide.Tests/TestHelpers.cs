using AudioGuide.Api.Common;
using AudioGuide.Api.Models;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AudioGuide.Tests
{
    public static class TestHelpers
    {
        // AutoMapper thật với các Profile của project (kiểm tra luôn quy tắc mapping)
        public static IMapper Mapper { get; } = new MapperConfiguration(
            cfg => cfg.AddMaps(typeof(AudioGuide.Api.Services.PoiService).Assembly), NullLoggerFactory.Instance).CreateMapper();

        public static IOptions<T> Options<T>(T value) where T : class => Microsoft.Extensions.Options.Options.Create(value);

        public static IOptions<LocalizationSettings> Localization() => Options(new LocalizationSettings());

        public static Poi Poi(int id = 1, string name = "Cổng Tam Quan", string description = "Cổng chính của chùa.", string tips = "")
            => new()
            {
                Id = id, Code = $"POI_{id}", Name = name, Description = description, Tips = tips,
                Latitude = 16.1 + id * 0.001, Longitude = 108.27, IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };

        public static Language Language(string code, string translator = "", string voice = "voice") => new()
        {
            Code = code, Name = code, NativeName = code, TranslatorCode = translator.Length > 0 ? translator : code, TtsVoice = voice,
            IsActive = true
        };
    }

    // Đồng hồ giả: test chọn được thời điểm "bây giờ"
    public class FakeClock : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeClock(DateTime utc) => _now = new DateTimeOffset(utc, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}
