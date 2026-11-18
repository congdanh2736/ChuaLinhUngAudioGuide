using System.Text;
using System.Text.RegularExpressions;
using AudioGuide.Api.Common;
using AudioGuide.Api.Interface.Integrations;

namespace AudioGuide.Api.Integrations
{
    /*
     * Giọng đọc DỰ PHÒNG: Google Translate TTS (miễn phí, không cần key, gọi HTTP GET bình thường).
     * Dùng khi Edge-TTS lỗi (Microsoft hay chặn 403 với IP của máy chủ hosting dùng chung).
     *   - Mỗi lần gọi chỉ đọc được ~200 ký tự -> cắt văn bản theo câu / dấu phẩy / khoảng trắng.
     *   - Mỗi đoạn trả về 1 file mp3 -> nối các file lại thành 1 file (trình duyệt phát bình thường).
     *   - Chỉ có 1 giọng mặc định cho mỗi ngôn ngữ (không chọn được giọng như Edge).
     */
    public partial class GoogleTranslateTts : ITtsProvider
    {
        private const int MaxCharsPerRequest = 180;
        private readonly HttpClient _http;
        private readonly ILogger<GoogleTranslateTts> _logger;

        public GoogleTranslateTts(HttpClient http, ILogger<GoogleTranslateTts> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<byte[]> SynthesizeAsync(string text, string voice, CancellationToken ct = default)
        {
            var clean = EdgeTtsClient.CleanForSpeech(text);
            if (clean.Length == 0) throw new ExternalServiceException("Không có nội dung để đọc", "TTS_FAILED");
            var lang = LangFromVoice(voice);

            using var output = new MemoryStream();
            var chunks = SplitForSpeech(clean, MaxCharsPerRequest);
            for (var i = 0; i < chunks.Count; i++)
            {
                var url = "https://translate.google.com/translate_tts?ie=UTF-8&client=tw-ob"
                          + $"&tl={Uri.EscapeDataString(lang)}&total={chunks.Count}&idx={i}"
                          + $"&textlen={chunks[i].Length}&q={Uri.EscapeDataString(chunks[i])}";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36");
                request.Headers.TryAddWithoutValidation("Referer", "https://translate.google.com/");

                HttpResponseMessage response;
                try
                {
                    response = await _http.SendAsync(request, ct);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    throw new ExternalServiceException($"Google TTS lỗi mạng: {ex.Message}", "TTS_FAILED");
                }
                using (response)
                {
                    if (!response.IsSuccessStatusCode)
                        throw new ExternalServiceException($"Google TTS lỗi HTTP {(int)response.StatusCode}", "TTS_FAILED");
                    var data = await response.Content.ReadAsByteArrayAsync(ct);
                    if (data.Length == 0) throw new ExternalServiceException("Google TTS trả về audio rỗng", "TTS_FAILED");
                    output.Write(data);
                }
                if (i < chunks.Count - 1) await Task.Delay(150, ct); // gọi dồn dập dễ bị chặn
            }
            _logger.LogInformation("Google TTS: {Lang}, {Chunks} đoạn, {Bytes} byte", lang, chunks.Count, output.Length);
            return output.ToArray();
        }

        // "ja-JP-NanamiNeural" -> "ja"; tiếng Trung giữ vùng: "zh-CN-XiaoxiaoNeural" -> "zh-CN"
        public static string LangFromVoice(string voice)
        {
            var parts = voice.Split('-');
            if (parts.Length < 2) return voice;
            return parts[0].Equals("zh", StringComparison.OrdinalIgnoreCase) ? $"{parts[0]}-{parts[1]}" : parts[0];
        }

        // Cắt thành các đoạn <= limit ký tự: ưu tiên ranh giới câu, rồi dấu phẩy, rồi khoảng trắng, cuối cùng cắt cứng
        public static List<string> SplitForSpeech(string text, int limit)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            foreach (var piece in SentenceRegex().Split(text).Where(p => p.Length > 0))
            {
                foreach (var part in BreakLong(piece, limit))
                {
                    if (current.Length + part.Length > limit && current.Length > 0)
                    {
                        result.Add(current.ToString().Trim());
                        current.Clear();
                    }
                    current.Append(part);
                }
            }
            if (current.ToString().Trim().Length > 0) result.Add(current.ToString().Trim());
            return result.Where(s => s.Length > 0).ToList();
        }

        private static IEnumerable<string> BreakLong(string piece, int limit)
        {
            if (piece.Length <= limit) { yield return piece; yield break; }
            var rest = piece;
            while (rest.Length > limit)
            {
                var cut = rest.LastIndexOf(' ', limit - 1);
                if (cut < limit / 2) cut = limit;               // không có khoảng trắng hợp lý (tiếng Nhật, Trung) -> cắt cứng
                yield return rest[..cut];
                rest = rest[cut..];
            }
            if (rest.Length > 0) yield return rest;
        }

        // Tách sau dấu kết thúc câu / dấu phẩy (giữ lại dấu): . ! ? ; , 。 ！ ？ ； 、 ，
        [GeneratedRegex(@"(?<=[\.!\?;,。！？；、，])")]
        private static partial Regex SentenceRegex();
    }

    /*
     * Thử lần lượt từng nhà cung cấp giọng đọc: Edge (giọng hay) -> Google (dự phòng).
     * Tất cả đều lỗi thì ném lỗi cuối cùng -> Service ghi lỗi, app du khách đọc bằng giọng trình duyệt.
     */
    public class FallbackTts : ITtsProvider
    {
        private readonly IReadOnlyList<(string Name, ITtsProvider Provider)> _providers;
        private readonly ILogger<FallbackTts> _logger;

        public FallbackTts(IReadOnlyList<(string Name, ITtsProvider Provider)> providers, ILogger<FallbackTts> logger)
        {
            _providers = providers;
            _logger = logger;
        }

        public async Task<byte[]> SynthesizeAsync(string text, string voice, CancellationToken ct = default)
        {
            var errors = new List<string>();
            foreach (var (name, provider) in _providers)
            {
                try
                {
                    return await provider.SynthesizeAsync(text, voice, ct);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning("TTS {Name} lỗi, thử dịch vụ kế tiếp: {Message}", name, ex.Message);
                    errors.Add($"{name}: {ex.Message}");
                }
            }
            throw new ExternalServiceException("Tạo audio lỗi - " + string.Join(" | ", errors), "TTS_FAILED");
        }
    }
}
