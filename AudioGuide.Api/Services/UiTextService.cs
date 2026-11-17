using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Content;
using AudioGuide.Api.Interface.Integrations;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Services.Logic;

namespace AudioGuide.Api.Services
{
    /*
     * Dịch chuỗi giao diện (nút bấm, tab, thông báo) của app du khách sang ngôn ngữ khách chọn.
     * - Tiếng Anh là bản gốc, tiếng Việt viết tay (Data/SeedData/UiStringsEn.json, UiStringsVi.json).
     * - Ngôn ngữ khác: lần đầu có khách dùng thì dịch toàn bộ 1 lần (gộp thành 1 lần gọi cho nhanh) rồi lưu DB.
     * - Chuỗi gốc đổi (SourceHash khác) -> dịch lại. Dịch lỗi -> trả tiếng Anh, lần sau thử lại.
     */
    public class UiTextService : IUiTextService
    {
        public static readonly Dictionary<string, string> StringsEn = LoadEmbedded("UiStringsEn.json");
        public static readonly Dictionary<string, string> StringsVi = LoadEmbedded("UiStringsVi.json");
        private static readonly string SourceHash = TextTools.Md5(JsonSerializer.Serialize(new SortedDictionary<string, string>(StringsEn)));
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(); // tránh 2 request cùng dịch 1 ngôn ngữ

        private readonly IUiTranslationRepository _repository;
        private readonly ILanguageRepository _languageRepository;
        private readonly ITranslator _translator;

        public UiTextService(IUiTranslationRepository repository, ILanguageRepository languageRepository, ITranslator translator)
        {
            _repository = repository;
            _languageRepository = languageRepository;
            _translator = translator;
        }

        private static Dictionary<string, string> LoadEmbedded(string name)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SeedData." + name)
                               ?? throw new InvalidOperationException($"Thiếu file dữ liệu nhúng SeedData.{name}");
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new();
        }

        private async Task<Dictionary<string, string>> TranslateAllAsync(string translatorCode)
        {
            var keys = StringsEn.Keys.ToList();
            // Gộp tất cả chuỗi thành 1 đoạn, mỗi dòng 1 chuỗi -> chỉ 1 lần gọi dịch
            var joined = string.Join("\n", keys.Select(k => StringsEn[k]));
            var lines = (await _translator.TranslateAsync(joined, translatorCode, "en"))
                .Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            if (lines.Count == keys.Count)
                return keys.Zip(lines).ToDictionary(x => x.First, x => x.Second);
            // Dịch máy làm lệch số dòng -> dịch từng chuỗi (chậm hơn nhưng chắc chắn)
            var result = new Dictionary<string, string>();
            foreach (var key in keys)
                result[key] = await _translator.TranslateAsync(StringsEn[key], translatorCode, "en");
            return result;
        }

        private static Dictionary<string, string> Merge(Dictionary<string, string> translated)
        {
            var merged = new Dictionary<string, string>(StringsEn);
            foreach (var (k, v) in translated) merged[k] = v;
            return merged;
        }

        public async Task<UiStringsDto> GetStringsAsync(string lang)
        {
            if (lang == "en") return new UiStringsDto { Lang = "en", Strings = StringsEn };
            if (lang == "vi") return new UiStringsDto { Lang = "vi", Strings = StringsVi };
            var language = await _languageRepository.GetByCodeAsync(lang);
            if (language == null || !language.IsActive) return new UiStringsDto { Lang = "en", Strings = StringsEn, IsFallback = true };

            var cached = await _repository.GetAsync(lang);
            if (cached != null && cached.SourceHash == SourceHash)
                return new UiStringsDto { Lang = lang, Strings = Merge(JsonSerializer.Deserialize<Dictionary<string, string>>(cached.StringsJson)!) };

            var gate = Locks.GetOrAdd(lang, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync();
            try
            {
                cached = await _repository.GetAsync(lang); // request khác có thể vừa dịch xong
                if (cached != null && cached.SourceHash == SourceHash)
                    return new UiStringsDto { Lang = lang, Strings = Merge(JsonSerializer.Deserialize<Dictionary<string, string>>(cached.StringsJson)!) };
                Dictionary<string, string> strings;
                try
                {
                    strings = await TranslateAllAsync(language.TranslatorCode);
                }
                catch (ExternalServiceException)
                {
                    return new UiStringsDto { Lang = lang, Strings = StringsEn, IsFallback = true };
                }
                await _repository.SaveAsync(lang, JsonSerializer.Serialize(strings), SourceHash);
                return new UiStringsDto { Lang = lang, Strings = Merge(strings) };
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
