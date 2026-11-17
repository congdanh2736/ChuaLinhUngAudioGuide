using System.Security.Cryptography;
using System.Text;
using AudioGuide.Api.Interface.Integrations;
using AudioGuide.Api.Interface.Repositories;

namespace AudioGuide.Api.Integrations
{
    /*
     * Lưu file audio ngay trong SQL Server (bảng MediaFiles) -> khởi động lại hay đổi máy đều còn.
     * Tên file = MD5(giọng + nội dung) -> cùng nội dung thì dùng lại, không tạo audio lần 2.
     */
    public class DatabaseMediaStorage : IMediaStorage
    {
        public const string UrlPrefix = "/media";
        private readonly IMediaFileRepository _repository;

        public DatabaseMediaStorage(IMediaFileRepository repository) => _repository = repository;

        public string AudioKey(string text, string voice)
            => "audio/" + Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes($"{voice}:{text}"))).ToLowerInvariant() + ".mp3";

        public string UrlFor(string key) => $"{UrlPrefix}/{key}";

        public Task<bool> ExistsAsync(string key) => _repository.ExistsAsync(key);

        public async Task<string> SaveAsync(string key, byte[] data, string? contentType = null)
        {
            await _repository.SaveAsync(key, data, contentType ?? GuessType(key));
            return UrlFor(key);
        }

        public async Task<(byte[] Data, string ContentType)?> GetAsync(string key)
        {
            var file = await _repository.GetAsync(key);
            return file == null ? null : (file.Data, file.ContentType);
        }

        public static string GuessType(string key) => Path.GetExtension(key).ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
