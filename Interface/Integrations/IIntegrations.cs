namespace AudioGuide.Api.Interface.Integrations
{
    /*
     * Dịch vụ bên ngoài (thuộc lớp 3 - Data Access giống Repository, nhưng nguồn dữ liệu không phải database).
     * Service chỉ biết tới các interface này -> muốn đổi Google sang DeepL, Edge-TTS sang Azure...
     * chỉ cần viết class mới, không phải sửa Service. Khi test thì thay bằng bản giả.
     */

    // Dịch máy
    public interface ITranslator
    {
        Task<string> TranslateAsync(string text, string target, string source = "vi", CancellationToken ct = default);
    }

    // Chuyển văn bản thành giọng nói (file mp3)
    public interface ITtsProvider
    {
        Task<byte[]> SynthesizeAsync(string text, string voice, CancellationToken ct = default);
    }

    public record LlmMessage(string Role, string Content);

    // Mô hình ngôn ngữ lớn (AI tạo sinh) - dùng cho chatbot và gợi ý mô tả
    public interface ILlmClient
    {
        bool IsEnabled { get; }
        string Describe { get; } // vd "openrouter / openai/gpt-4o-mini" (ghi log)
        Task<string> CompleteAsync(IReadOnlyList<LlmMessage> messages, double temperature = 0.3, int? maxTokens = null,
            CancellationToken ct = default);
    }

    // Cổng thanh toán (hiện là cổng giả lập thay cho Payoo/VNPay)
    public interface IPaymentGateway
    {
        string Name { get; }
        string CreatePaymentUrl(int paymentId, int amount);
        string Sign(IDictionary<string, string?> payload);
        bool VerifyWebhook(IDictionary<string, string?> payload, string signature);
    }

    // Nơi lưu file audio (mặc định trong SQL Server, bảng MediaFiles)
    public interface IMediaStorage
    {
        string AudioKey(string text, string voice);
        string UrlFor(string key);
        Task<bool> ExistsAsync(string key);
        Task<string> SaveAsync(string key, byte[] data, string? contentType = null);
        Task<(byte[] Data, string ContentType)?> GetAsync(string key);
    }
}
