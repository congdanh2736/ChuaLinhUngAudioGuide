using System.Globalization;
using System.Net.WebSockets;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AudioGuide.Api.Common;
using AudioGuide.Api.Interface.Integrations;

namespace AudioGuide.Api.Integrations
{
    /*
     * Chuyển văn bản thành giọng nói bằng Microsoft Edge "Read Aloud" (miễn phí, không cần key) - viết lại
     * giao thức của thư viện Python edge-tts:
     *   1. Mở WebSocket tới speech.platform.bing.com kèm mã Sec-MS-GEC (SHA-256 của thời gian + token công khai).
     *   2. Gửi cấu hình (định dạng mp3) và nội dung SSML (giọng đọc + văn bản).
     *   3. Nhận nhiều gói nhị phân "Path:audio" -> ghép lại thành file mp3, tới "Path:turn.end" thì xong.
     * Dịch vụ không chính thức: Microsoft đổi giao thức thì có thể lỗi -> Service bắt lỗi, bỏ qua audio,
     * app du khách tự đọc bằng giọng của trình duyệt.
     */
    public partial class EdgeTtsClient : ITtsProvider
    {
        private const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
        private const string ChromiumFullVersion = "140.0.3485.14";
        private const string ChromiumMajorVersion = "140";
        private const string WssUrl =
            "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1?TrustedClientToken=" + TrustedClientToken;
        private const int MaxCharsPerRequest = 2500;

        private readonly ILogger<EdgeTtsClient> _logger;
        public EdgeTtsClient(ILogger<EdgeTtsClient> logger) => _logger = logger;

        public async Task<byte[]> SynthesizeAsync(string text, string voice, CancellationToken ct = default)
        {
            var clean = CleanForSpeech(text);
            if (clean.Length == 0) throw new ExternalServiceException("Không có nội dung để đọc", "TTS_FAILED");
            try
            {
                using var output = new MemoryStream();
                foreach (var chunk in TranslatorText.SplitText(clean, MaxCharsPerRequest))
                {
                    var audio = await SynthesizeChunkAsync(chunk, voice, ct);
                    output.Write(audio);
                }
                if (output.Length == 0) throw new ExternalServiceException("Edge-TTS trả về audio rỗng", "TTS_FAILED");
                return output.ToArray();
            }
            catch (ExternalServiceException)
            {
                throw;
            }
            catch (Exception ex) when (ex is WebSocketException or IOException or OperationCanceledException or InvalidOperationException)
            {
                _logger.LogWarning("Edge-TTS lỗi (voice={Voice}): {Message}", voice, ex.Message);
                throw new ExternalServiceException($"Tạo audio lỗi: {ex.Message}", "TTS_FAILED");
            }
        }

        private static async Task<byte[]> SynthesizeChunkAsync(string text, string voice, CancellationToken ct)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            var token = timeout.Token;

            using var ws = new ClientWebSocket();
            ws.Options.SetRequestHeader("Pragma", "no-cache");
            ws.Options.SetRequestHeader("Cache-Control", "no-cache");
            ws.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
            ws.Options.SetRequestHeader("User-Agent",
                $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{ChromiumMajorVersion}.0.0.0 Safari/537.36 Edg/{ChromiumMajorVersion}.0.0.0");
            ws.Options.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
            ws.Options.SetRequestHeader("Cookie", $"muid={Convert.ToHexString(RandomNumberGenerator.GetBytes(16))};");

            var url = $"{WssUrl}&ConnectionId={NewId()}&Sec-MS-GEC={SecMsGec()}&Sec-MS-GEC-Version=1-{ChromiumFullVersion}";
            await ws.ConnectAsync(new Uri(url), token);

            var config = $"X-Timestamp:{Timestamp()}\r\nContent-Type:application/json; charset=utf-8\r\nPath:speech.config\r\n\r\n"
                         + "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{\"sentenceBoundaryEnabled\":\"false\","
                         + "\"wordBoundaryEnabled\":\"false\"},\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}\r\n";
            await SendTextAsync(ws, config, token);

            var ssml = "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>"
                       + $"<voice name='{LongVoiceName(voice)}'><prosody pitch='+0Hz' rate='+0%' volume='+0%'>"
                       + SecurityElement.Escape(text) + "</prosody></voice></speak>";
            var request = $"X-RequestId:{NewId()}\r\nContent-Type:application/ssml+xml\r\nX-Timestamp:{Timestamp()}Z\r\nPath:ssml\r\n\r\n{ssml}";
            await SendTextAsync(ws, request, token);

            using var audio = new MemoryStream();
            var buffer = new byte[64 * 1024];
            while (ws.State == WebSocketState.Open)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(buffer, token);
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close) break;
                var data = message.ToArray();
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    if (Encoding.UTF8.GetString(data).Contains("Path:turn.end")) break;
                    continue;
                }
                // Gói nhị phân: 2 byte đầu = độ dài phần header, sau header là dữ liệu mp3
                if (data.Length < 2) continue;
                var headerLength = (data[0] << 8) | data[1];
                if (data.Length < 2 + headerLength) continue;
                var header = Encoding.UTF8.GetString(data, 2, headerLength);
                if (header.Contains("Path:audio"))
                    audio.Write(data, 2 + headerLength, data.Length - 2 - headerLength);
            }
            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
            return audio.ToArray();
        }

        private static Task SendTextAsync(ClientWebSocket ws, string text, CancellationToken ct)
            => ws.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, ct);

        private static string NewId() => Guid.NewGuid().ToString("N");

        private static string Timestamp()
            => DateTime.UtcNow.ToString("ddd MMM dd yyyy HH:mm:ss", CultureInfo.InvariantCulture)
               + " GMT+0000 (Coordinated Universal Time)";

        // Mã chống lạm dụng của Microsoft: SHA-256(thời gian Windows làm tròn 5 phút + token), viết HOA
        private static string SecMsGec()
        {
            var seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 11644473600L; // đổi sang mốc thời gian Windows (1601)
            seconds -= seconds % 300;
            var ticks = seconds * 10_000_000L;                                          // đơn vị 100 nano giây
            var hash = SHA256.HashData(Encoding.ASCII.GetBytes($"{ticks}{TrustedClientToken}"));
            return Convert.ToHexString(hash);
        }

        // "vi-VN-HoaiMyNeural" -> "Microsoft Server Speech Text to Speech Voice (vi-VN, HoaiMyNeural)"
        public static string LongVoiceName(string voice)
        {
            var m = ShortVoiceRegex().Match(voice);
            return m.Success
                ? $"Microsoft Server Speech Text to Speech Voice ({m.Groups[1].Value}-{m.Groups[2].Value}, {m.Groups[3].Value})"
                : voice;
        }

        // Bỏ ký hiệu Markdown để máy không đọc ra dấu # hay *
        public static string CleanForSpeech(string text)
        {
            text = Regex.Replace(text, @"#+\s*", "");
            text = Regex.Replace(text, @"[*_`>]+", "");
            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        [GeneratedRegex(@"^([a-z]{2,})-([A-Z]{2,})-(.+Neural)$")]
        private static partial Regex ShortVoiceRegex();
    }

    public class DisabledTts : ITtsProvider
    {
        public Task<byte[]> SynthesizeAsync(string text, string voice, CancellationToken ct = default)
            => throw new ExternalServiceException("TTS đang tắt", "TTS_DISABLED");
    }
}
