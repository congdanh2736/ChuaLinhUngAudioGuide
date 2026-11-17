using System.Security.Cryptography;
using System.Text;
using AudioGuide.Api.Data;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AudioGuide.Api.Repositories
{
    // Bản dịch chuỗi giao diện app theo ngôn ngữ
    public class UiTranslationRepository : IUiTranslationRepository
    {
        private readonly ApplicationDbContext _context;
        public UiTranslationRepository(ApplicationDbContext context) => _context = context;

        public async Task<UiTranslation?> GetAsync(string lang)
            => await _context.UiTranslations.AsNoTracking().FirstOrDefaultAsync(x => x.Lang == lang);

        public async Task SaveAsync(string lang, string stringsJson, string sourceHash)
        {
            var row = await _context.UiTranslations.FirstOrDefaultAsync(x => x.Lang == lang);
            if (row == null)
            {
                row = new UiTranslation { Lang = lang };
                _context.UiTranslations.Add(row);
            }
            row.StringsJson = stringsJson;
            row.SourceHash = sourceHash;
            row.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    /*
     * Cache câu trả lời chatbot lưu trong bảng ChatCache.
     * Bản ghi quá hạn bị bỏ qua khi đọc và được dọn mỗi lần ghi -> bảng không phình to.
     */
    public class ChatCacheRepository : IChatCacheRepository
    {
        private readonly ApplicationDbContext _context;
        public ChatCacheRepository(ApplicationDbContext context) => _context = context;

        // Câu hỏi có thể rất dài -> dùng SHA-256 (64 ký tự) làm khóa chính
        private static string Hash(string key)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

        public async Task<string?> GetAsync(string key)
        {
            var hashed = Hash(key);
            var now = DateTime.UtcNow;
            return await _context.ChatCache.AsNoTracking()
                .Where(x => x.Key == hashed && x.ExpiresAt > now)
                .Select(x => x.AnswerJson)
                .FirstOrDefaultAsync();
        }

        public async Task SetAsync(string key, string answerJson, int ttlSeconds)
        {
            var now = DateTime.UtcNow;
            var hashed = Hash(key);
            await _context.ChatCache.Where(x => x.ExpiresAt <= now).ExecuteDeleteAsync();
            var row = await _context.ChatCache.FirstOrDefaultAsync(x => x.Key == hashed);
            if (row == null)
            {
                row = new ChatCacheEntry { Key = hashed };
                _context.ChatCache.Add(row);
            }
            row.AnswerJson = answerJson;
            row.ExpiresAt = now.AddSeconds(ttlSeconds);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // 2 người hỏi cùng 1 câu cùng lúc -> người kia đã lưu rồi, bỏ qua
                _context.Entry(row).State = EntityState.Detached;
            }
        }
    }

    // File audio lưu trong SQL Server (bảng MediaFiles, cột VARBINARY(MAX))
    public class MediaFileRepository : IMediaFileRepository
    {
        private readonly ApplicationDbContext _context;
        public MediaFileRepository(ApplicationDbContext context) => _context = context;

        public async Task<bool> ExistsAsync(string key)
            => await _context.MediaFiles.AnyAsync(m => m.Key == key);

        public async Task<MediaFile?> GetAsync(string key)
            => await _context.MediaFiles.AsNoTracking().FirstOrDefaultAsync(m => m.Key == key);

        public async Task SaveAsync(string key, byte[] data, string contentType)
        {
            var row = await _context.MediaFiles.FirstOrDefaultAsync(m => m.Key == key);
            if (row == null)
            {
                row = new MediaFile { Key = key };
                _context.MediaFiles.Add(row);
            }
            row.Data = data;
            row.ContentType = contentType;
            row.Size = data.Length;
            row.UpdatedAt = DateTime.UtcNow;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Tên file = hash nội dung -> tác vụ khác vừa lưu đúng file này rồi
                _context.Entry(row).State = EntityState.Detached;
            }
        }
    }
}
