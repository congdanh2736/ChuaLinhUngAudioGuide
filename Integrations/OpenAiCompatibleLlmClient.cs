using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AudioGuide.Api.Common;
using AudioGuide.Api.Interface.Integrations;
using Microsoft.Extensions.Options;

namespace AudioGuide.Api.Integrations
{
    /*
     * Gọi AI tạo sinh (LLM). Hầu hết nhà cung cấp có API "tương thích OpenAI" (cùng định dạng /chat/completions),
     * nên chỉ cần 1 client, đổi BaseUrl + Model là dùng được:
     *   openrouter : OpenRouter - nhiều model, có model miễn phí (đuôi ":free"), key dạng sk-or-...
     *   gemini     : Google Gemini - có gói miễn phí, key tại https://aistudio.google.com/apikey
     *   groq       : Groq (Llama) - có gói miễn phí, key dạng gsk_...
     * Llm:Provider = auto sẽ tự nhận nhà cung cấp theo dạng key.
     */
    public class OpenAiCompatibleLlmClient : ILlmClient
    {
        private static readonly Dictionary<string, (string BaseUrl, string Model)> Presets = new()
        {
            ["openrouter"] = ("https://openrouter.ai/api/v1", "openai/gpt-4o-mini"),
            ["gemini"] = ("https://generativelanguage.googleapis.com/v1beta/openai", "gemini-3.8-flash"),
            ["groq"] = ("https://api.groq.com/openai/v1", "llama-3.3-70b-versatile"),
        };
        private static readonly HashSet<HttpStatusCode> Retryable = new()
        {
            HttpStatusCode.InternalServerError, HttpStatusCode.BadGateway, HttpStatusCode.ServiceUnavailable, HttpStatusCode.GatewayTimeout
        };
        private static readonly int[] RetryDelaysMs = { 1000, 3000 };

        private readonly HttpClient _http;
        private readonly ILogger<OpenAiCompatibleLlmClient> _logger;
        private readonly string? _apiKey;
        private readonly string _provider = "";
        private readonly string _baseUrl = "";
        private string _model = "";
        private bool _sendReasoningEffort;   // Gemini đời mới "suy nghĩ" lâu -> giảm xuống mức thấp
        private bool _modelChecked;          // chỉ tự dò model thay thế (khi 404) 1 lần
        private List<string>? _modelIds;     // cache danh sách model của nhà cung cấp

        public OpenAiCompatibleLlmClient(HttpClient http, IOptions<LlmSettings> options, ILogger<OpenAiCompatibleLlmClient> logger)
        {
            _http = http;
            _logger = logger;
            var s = options.Value;
            _http.Timeout = TimeSpan.FromSeconds(Math.Max(5, s.TimeoutSeconds));

            // Bỏ khoảng trắng / dấu nháy hay bị dính khi copy key
            var key = (s.ApiKey ?? "").Trim().Trim('"', '\'');
            var providerSetting = (s.Provider ?? "auto").Trim().ToLowerInvariant();
            if (providerSetting == "none" || key.Length == 0) return;

            var modelSetting = (s.Model ?? "").Trim();
            if (Presets.ContainsKey(modelSetting.ToLowerInvariant()))
            {
                // Lỗi hay gặp: ghi Model = "gemini" (tên nhà cung cấp) thay vì Provider = "gemini"
                if (!Presets.ContainsKey(providerSetting)) providerSetting = modelSetting.ToLowerInvariant();
                modelSetting = "";
            }
            _provider = Presets.ContainsKey(providerSetting) ? providerSetting : DetectProvider(key);
            var preset = Presets[_provider];
            _apiKey = key;
            _baseUrl = (string.IsNullOrWhiteSpace(s.BaseUrl) ? preset.BaseUrl : s.BaseUrl).TrimEnd('/');
            _model = modelSetting.Length > 0 ? modelSetting : preset.Model;
            _sendReasoningEffort = _provider == "gemini";
        }

        public bool IsEnabled => _apiKey != null;
        public string Describe => IsEnabled ? $"{_provider} / {_model}" : "tắt";

        // Đoán nhà cung cấp theo đầu key. Key không nhận ra được coi là Gemini.
        public static string DetectProvider(string apiKey)
        {
            if (apiKey.StartsWith("gsk_")) return "groq";
            if (apiKey.StartsWith("sk-or-")) return "openrouter";
            return "gemini";
        }

        public async Task<string> CompleteAsync(IReadOnlyList<LlmMessage> messages, double temperature = 0.3, int? maxTokens = null,
            CancellationToken ct = default)
        {
            if (!IsEnabled) throw new ExternalServiceException("Chưa cấu hình AI (thiếu Llm:ApiKey)", "LLM_NOT_CONFIGURED");
            string content;
            try
            {
                var model = _model;
                var response = await PostWithRetryAsync(model, messages, temperature, maxTokens, ct);

                // 400 khi có tham số riêng -> model không hỗ trợ: bỏ tham số đó rồi gọi lại
                if (response.StatusCode == HttpStatusCode.BadRequest && _sendReasoningEffort)
                {
                    _sendReasoningEffort = false;
                    response = await PostWithRetryAsync(model, messages, temperature, maxTokens, ct);
                }

                // 404 = model đã bị gỡ / đổi tên -> tự dò model khác còn dùng được, đổi hẳn
                if (response.StatusCode == HttpStatusCode.NotFound && !_modelChecked)
                {
                    _modelChecked = true;
                    var replacement = (await RankedModelsAsync(model, ct)).FirstOrDefault();
                    if (replacement != null)
                    {
                        _logger.LogWarning("Model {Old} không còn dùng được, tự chuyển sang {New}", model, replacement);
                        _model = model = replacement;
                        response = await PostWithRetryAsync(model, messages, temperature, maxTokens, ct);
                    }
                }

                // Vẫn quá tải sau khi thử lại -> TẠM dùng model dự phòng cho câu này
                if (Retryable.Contains(response.StatusCode))
                {
                    foreach (var backup in (await RankedModelsAsync(_model, ct)).Take(2))
                    {
                        _logger.LogWarning("Model {Model} đang quá tải, tạm dùng {Backup}", _model, backup);
                        response = await PostAsync(backup, messages, temperature, maxTokens, ct);
                        if (response.IsSuccessStatusCode) break;
                    }
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                {
                    // Ghi rõ mã lỗi + lời nhắn của nhà cung cấp để biết sai key, sai model hay hết lượt
                    _logger.LogWarning("Gọi AI lỗi HTTP {Status} ({BaseUrl}, model={Model}): {Body}",
                        (int)response.StatusCode, _baseUrl, _model, body.Length > 300 ? body[..300] : body);
                    if ((int)response.StatusCode == 429)
                        throw new ExternalServiceException("Dịch vụ AI đang hết lượt, thử lại sau", "LLM_RATE_LIMITED");
                    if (Retryable.Contains(response.StatusCode))
                        throw new ExternalServiceException("Dịch vụ AI đang quá tải, thử lại sau", "LLM_BUSY");
                    throw new ExternalServiceException("Dịch vụ AI tạm thời không phản hồi", "LLM_FAILED");
                }
                using var doc = JsonDocument.Parse(body);
                content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
            }
            catch (ExternalServiceException)
            {
                throw;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("AI ({Model}) trả lời quá chậm, bỏ qua", _model);
                throw new ExternalServiceException("Dịch vụ AI phản hồi quá chậm", "LLM_TIMEOUT");
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException
                                           or IndexOutOfRangeException)
            {
                _logger.LogWarning("Gọi AI ({Model}) lỗi: {Message}", _model, ex.Message);
                throw new ExternalServiceException("Dịch vụ AI tạm thời không phản hồi", "LLM_FAILED");
            }
            if (string.IsNullOrWhiteSpace(content))
                throw new ExternalServiceException("Dịch vụ AI trả về câu rỗng", "LLM_FAILED");
            return content.Trim();
        }

        private async Task<HttpResponseMessage> PostAsync(string model, IReadOnlyList<LlmMessage> messages, double temperature,
            int? maxTokens, CancellationToken ct)
        {
            var body = new JsonObject
            {
                ["model"] = model,
                ["temperature"] = temperature,
                ["messages"] = new JsonArray(messages.Select(m => (JsonNode)new JsonObject
                {
                    ["role"] = m.Role,
                    ["content"] = m.Content
                }).ToArray())
            };
            if (maxTokens.HasValue) body["max_tokens"] = maxTokens.Value;
            if (_sendReasoningEffort) body["reasoning_effort"] = "low";

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions")
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            return await _http.SendAsync(request, ct);
        }

        // Nhà cung cấp quá tải (5xx) hoặc rớt mạng -> chờ một chút rồi thử lại
        private async Task<HttpResponseMessage> PostWithRetryAsync(string model, IReadOnlyList<LlmMessage> messages, double temperature,
            int? maxTokens, CancellationToken ct)
        {
            for (var attempt = 0; ; attempt++)
            {
                var isLast = attempt >= RetryDelaysMs.Length;
                try
                {
                    var response = await PostAsync(model, messages, temperature, maxTokens, ct);
                    if (!Retryable.Contains(response.StatusCode) || isLast) return response;
                    _logger.LogInformation("AI quá tải (HTTP {Status}), thử lại...", (int)response.StatusCode);
                }
                catch (HttpRequestException) when (!isLast)
                {
                    _logger.LogInformation("Mất kết nối tới AI, thử lại...");
                }
                await Task.Delay(RetryDelaysMs[attempt], ct);
            }
        }

        // Danh sách model dùng để chat của nhà cung cấp, xếp theo mức phù hợp (lấy 1 lần rồi cache)
        private async Task<List<string>> RankedModelsAsync(string exclude, CancellationToken ct)
        {
            if (_modelIds == null)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/models");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                    var response = await _http.SendAsync(request, ct);
                    response.EnsureSuccessStatusCode();
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                    _modelIds = doc.RootElement.GetProperty("data").EnumerateArray()
                        .Select(m => (m.GetProperty("id").GetString() ?? "").Replace("models/", ""))
                        .Where(id => id.Length > 0)
                        .ToList();
                }
                catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or TaskCanceledException)
                {
                    _logger.LogWarning("Không lấy được danh sách model: {Message}", ex.Message);
                    return new List<string>();
                }
            }
            return RankModels(_modelIds.Where(m => m != exclude), _model);
        }

        private static readonly string[] SkipWords =
            { "embedding", "image", "tts", "audio", "live", "vision", "aqa", "imagen", "veo", "robotics", "computer" };

        // Xếp model: cùng "dòng" với model đang cấu hình (flash/pro/mini...) -> bản ổn định -> phiên bản mới hơn
        public static List<string> RankModels(IEnumerable<string> ids, string preferred)
        {
            var family = new[] { "flash-lite", "flash", "pro", "mini" }
                .FirstOrDefault(f => preferred.Contains(f, StringComparison.OrdinalIgnoreCase)) ?? "flash";
            return ids
                .Where(m => !SkipWords.Any(w => m.Contains(w, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(m =>
                {
                    var name = m.ToLowerInvariant();
                    return name.Contains(family) && (family == "flash-lite" || !name.Contains("lite"));
                })
                .ThenByDescending(m => !new[] { "preview", "exp", "latest" }.Any(w => m.Contains(w, StringComparison.OrdinalIgnoreCase)))
                .ThenByDescending(m =>
                {
                    var match = Regex.Match(m, @"\d+(?:\.\d+)?");
                    return match.Success ? double.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
                })
                .ThenBy(m => m.Length)
                .ToList();
        }
    }
}
