using AudioGuide.Api.Data;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Models;
using AudioGuide.Api.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace AudioGuide.Api.Repositories
{
    public class VisitEventRepository : Repository<VisitEvent>, IVisitEventRepository
    {
        public VisitEventRepository(ApplicationDbContext context) : base(context) { }

        public async Task<int> CountForPoiAsync(int poiId)
            => await _set.CountAsync(e => e.PoiId == poiId);

        // Các điểm được quan tâm nhiều nhất (GROUP BY PoiId ORDER BY COUNT(*) DESC)
        public async Task<List<(int PoiId, int Count)>> TopPoisAsync(int limit)
        {
            var rows = await _set.GroupBy(e => e.PoiId)
                .Select(g => new { PoiId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(limit)
                .ToListAsync();
            return rows.Select(r => (r.PoiId, r.Count)).ToList();
        }

        // Chỉ lấy cột thời gian (nhẹ) để tính "khung giờ đông khách"
        public async Task<List<DateTime>> TimesForPoiAsync(int poiId, DateTime since)
            => await _set.Where(e => e.PoiId == poiId && e.CreatedAt >= since)
                .Select(e => e.CreatedAt)
                .ToListAsync();

        public async Task<Dictionary<string, int>> CountByLangAsync()
            => await _set.GroupBy(e => e.Lang)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
    }
}
