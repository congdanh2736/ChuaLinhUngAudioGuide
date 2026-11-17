using System.Collections.Concurrent;
using System.Text.Json;
using AudioGuide.Api.Common;
using AudioGuide.Api.Interface.Integrations;

namespace AudioGuide.Api.Integrations
{
    /*
     * Dịch máy MIỄN PHÍ. Các dịch vụ miễn phí đều giới hạn số lần gọi, gọi dồn dập sẽ bị chặn tạm (HTTP 429).
     * FallbackTranslator xử lý việc đó:
     *  - Giãn nhịp: mỗi dịch vụ chỉ nhận 1 yêu cầu mỗi ~0.6 giây.
     *  - Dịch vụ báo bị chặn -> "nghỉ" dịch vụ đó 10 phút, tự chuyển sang dịch vụ kế tiếp.
     *  - Thứ tự: Google (translate.googleapis.com) -> MyMemory.
     */
    public static class TranslatorText
    {
        public const string RateLimited = "TRANSLATION_RATE_LIMITED";

        // Cắt văn bản dài theo đoạn / câu để không vượt giới hạn ký tự mỗi lần gọi
        public static List<string> SplitText(string text, int limit)
        {
            if (text.Length <= limit) return new List<string> { text };
            var chunks = new List<string>();
            var current = "";
            foreach (var part in text.Replace("\n", "\n\u0001").Split('\u0001'))
            {
                var sentences = part.Length > limit ? part.Split(". ") : new[] { part };
                for (var i = 0; i < sentences.Length; i++)
                {
                    var piece = sentences[i] + (i < sentences.Length - 1 ? ". " : "");
                    if (current.Length + piece.Length > limit && current.Length > 0)
                    {
                        chunks.Add(current);
                        current = "";
                    }
                    current += piece;
                }
            }
            if (current.Length > 0) chunks.Add(current);
            return chunks;
        }

        // Gom các dòng thành nhóm <= limit ký tự (giữ ranh giới dòng để ghép lại đúng số dòng)
        public static List<string> ChunkLines(string text, int limit)
        {
            var groups = new List<string>();
            var current = new List<string>();
            var size = 0;
            foreach (var line in text.Split('\n'))
            {
                if (current.Count > 0 && size + line.Length + 1 > limit)
                {
                    groups.Add(string.Join("\n", current));
                    current.Clear();
                    size = 0;
                }
                current.Add(line);
                size += line.Length + 1;
            }
            if (current.Count > 0) groups.Add(string.Join("\n", current));
            return groups;
        }

        public static async Task<string> TranslateInGroupsAsync(string text, int limit, Func<string, Task<string>> translateOne)
        {
            var output = new List<string>();
            foreach (var group in ChunkLines(text, limit))
            {
                if (group.Length <= limit)
                {
                    output.Add(await translateOne(group));
                }
                else
                {
                    var pieces = new List<string>();
                    foreach (var piece in SplitText(group, limit))
                        pieces.Add((await translateOne(piece)).Trim());
                    output.Add(string.Join(" ", pieces));
                }
            }
            return string.Join("\n", output);
        }
    }

    public interface INamedTranslator : ITranslator
    {
        string Name { get; }
    }

    // Google Translate qua endpoint công khai translate.googleapis.com (client=gtx)
    public class GoogleApiTranslator : INamedTranslator
    {
        private readonly HttpClient _http;
        public string Name => "google";
        public GoogleApiTranslator(HttpClient http) => _http = http;

        public async Task<string> TranslateAsync(string text, string target, string source = "vi", CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text) || target == source) return text;
            return await TranslatorText.TranslateInGroupsAsync(text, 1500, async chunk =>
            {
                var url = "https://translate.googleapis.com/translate_a/single?client=gtx&dt=t"
                          + $"&sl={Uri.EscapeDataString(source)}&tl={Uri.EscapeDataString(target)}&q={Uri.EscapeDataString(chunk)}";
                HttpResponseMessage response;
                try
                {
                    response = await _http.GetAsync(url, ct);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    throw new ExternalServiceException($"Google lỗi mạng: {ex.Message}", "TRANSLATION_FAILED");
                }
                if ((int)response.StatusCode == 429)
                    throw new ExternalServiceException("Google tạm chặn vì gọi quá nhiều", TranslatorText.RateLimited);
                if (!response.IsSuccessStatusCode)
                    throw new ExternalServiceException($"Google lỗi HTTP {(int)response.StatusCode}", "TRANSLATION_FAILED");
                try
                {
                    // Kết quả dạng [[["câu đã dịch","câu gốc",...], ...], ...]
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                    var parts = doc.RootElement[0].EnumerateArray()
                        .Select(seg => seg[0].ValueKind == JsonValueKind.String ? seg[0].GetString() : null)
                        .Where(s => !string.IsNullOrEmpty(s));
                    return string.Concat(parts);
                }
                catch (Exception ex) when (ex is JsonException or InvalidOperationException or IndexOutOfRangeException)
                {
                    throw new ExternalServiceException("Google trả dữ liệu lạ", "TRANSLATION_FAILED");
                }
            });
        }
    }

    // MyMemory (mymemory.translated.net) - miễn phí, ~5.000 ký tự/ngày; khai báo email được ~50.000
    public class MyMemoryTranslator : INamedTranslator
    {
        private readonly HttpClient _http;
        private readonly string? _email;
        public string Name => "mymemory";

        public MyMemoryTranslator(HttpClient http, string? email)
        {
            _http = http;
            _email = email;
        }

        public async Task<string> TranslateAsync(string text, string target, string source = "vi", CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text) || target == source) return text;
            return await TranslatorText.TranslateInGroupsAsync(text, 450, async chunk =>
            {
                var url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(chunk)}"
                          + $"&langpair={Uri.EscapeDataString(source + "|" + target)}";
                if (!string.IsNullOrWhiteSpace(_email)) url += "&de=" + Uri.EscapeDataString(_email);
                try
                {
                    var response = await _http.GetAsync(url, ct);
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                    var root = doc.RootElement;
                    var status = root.TryGetProperty("responseStatus", out var st) && st.ValueKind == JsonValueKind.Number
                        ? st.GetInt32() : (int)response.StatusCode;
                    var quotaFinished = root.TryGetProperty("quotaFinished", out var q) && q.ValueKind == JsonValueKind.True;
                    if (status == 429 || quotaFinished)
                        throw new ExternalServiceException("MyMemory hết lượt dịch hôm nay", TranslatorText.RateLimited);
                    if (status != 200)
                        throw new ExternalServiceException("MyMemory lỗi: " + root.GetProperty("responseDetails"), "TRANSLATION_FAILED");
                    return root.GetProperty("responseData").GetProperty("translatedText").GetString() ?? "";
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
                {
                    throw new ExternalServiceException($"MyMemory lỗi: {ex.Message}", "TRANSLATION_FAILED");
                }
            });
        }
    }

    // Bộ điều phối: thử lần lượt các dịch vụ, giãn nhịp gọi, tạm nghỉ dịch vụ bị chặn
    public class FallbackTranslator : ITranslator
    {
        private readonly IReadOnlyList<INamedTranslator> _providers;
        private readonly ILogger<FallbackTranslator> _logger;
        private readonly TimeSpan _minInterval = TimeSpan.FromMilliseconds(600);
        private readonly TimeSpan _cooldown = TimeSpan.FromMinutes(10);
        private readonly ConcurrentDictionary<string, DateTime> _blockedUntil = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastCall = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        public FallbackTranslator(IEnumerable<INamedTranslator> providers, ILogger<FallbackTranslator> logger)
        {
            _providers = providers.ToList();
            _logger = logger;
        }

        private async Task ThrottleAsync(string name, CancellationToken ct)
        {
            var gate = _locks.GetOrAdd(name, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(ct);
            try
            {
                var wait = _lastCall.GetValueOrDefault(name) + _minInterval - DateTime.UtcNow;
                if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
                _lastCall[name] = DateTime.UtcNow;
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task<string> TranslateAsync(string text, string target, string source = "vi", CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text) || target == source) return text;
            var errors = new List<string>();
            foreach (var provider in _providers)
            {
                if (_blockedUntil.TryGetValue(provider.Name, out var until) && until > DateTime.UtcNow)
                {
                    errors.Add($"{provider.Name}: đang tạm nghỉ do bị chặn");
                    continue;
                }
                await ThrottleAsync(provider.Name, ct);
                try
                {
                    return await provider.TranslateAsync(text, target, source, ct);
                }
                catch (ExternalServiceException ex)
                {
                    if (ex.Code == TranslatorText.RateLimited)
                    {
                        _blockedUntil[provider.Name] = DateTime.UtcNow + _cooldown;
                        _logger.LogWarning("{Provider} bị chặn (quá nhiều yêu cầu) -> nghỉ 10 phút, chuyển dịch vụ khác", provider.Name);
                    }
                    else
                    {
                        _logger.LogWarning("{Provider} dịch {Source}->{Target} lỗi: {Message}", provider.Name, source, target, ex.Message);
                    }
                    errors.Add($"{provider.Name}: {ex.Message}");
                }
            }
            throw new ExternalServiceException("Tất cả dịch vụ dịch đều lỗi - " + string.Join("; ", errors), "TRANSLATION_FAILED");
        }
    }

    // Khi tắt dịch máy (Translator:Provider = none): luôn báo lỗi để hệ thống dùng bản dự phòng
    public class DisabledTranslator : ITranslator
    {
        public Task<string> TranslateAsync(string text, string target, string source = "vi", CancellationToken ct = default)
        {
            if (target == source) return Task.FromResult(text);
            throw new ExternalServiceException("Dịch máy đang tắt", "TRANSLATION_DISABLED");
        }
    }
}
