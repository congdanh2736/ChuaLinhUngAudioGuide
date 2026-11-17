using AudioGuide.Api.Data;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Models;
using AudioGuide.Api.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace AudioGuide.Api.Repositories
{
    public class ChatLogRepository : Repository<ChatLog>, IChatLogRepository
    {
        public ChatLogRepository(ApplicationDbContext context) : base(context) { }

        public async Task<int> CountSinceAsync(int sessionId, DateTime since)
            => await _set.CountAsync(c => c.SessionId == sessionId && c.CreatedAt >= since);

        // Các lượt hỏi - đáp gần nhất của 1 phiên, xếp từ cũ đến mới (để AI hiểu câu hỏi nối tiếp)
        public async Task<List<ChatLog>> RecentForSessionAsync(int sessionId, int limit)
        {
            var items = await _set.AsNoTracking()
                .Where(c => c.SessionId == sessionId)
                .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
                .Take(limit)
                .ToListAsync();
            items.Reverse();
            return items;
        }

        public async Task<List<ChatLog>> RecentAsync(int limit)
            => await _set.AsNoTracking()
                .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
                .Take(limit)
                .ToListAsync();
    }
}
