using AudioGuide.Api.Data;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Models;
using AudioGuide.Api.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace AudioGuide.Api.Repositories
{
    public class PoiLocalizationRepository : Repository<PoiLocalization>, IPoiLocalizationRepository
    {
        public PoiLocalizationRepository(ApplicationDbContext context) : base(context) { }

        public async Task<PoiLocalization?> GetAsync(int poiId, string lang)
            => await _set.AsNoTracking().FirstOrDefaultAsync(x => x.PoiId == poiId && x.Lang == lang);

        public async Task<List<PoiLocalization>> ListForPoiAsync(int poiId)
            => await _set.AsNoTracking().Where(x => x.PoiId == poiId).ToListAsync();

        public async Task<List<PoiLocalization>> ListForLangsAsync(IEnumerable<string> langs, IEnumerable<int>? poiIds = null)
        {
            var langList = langs.ToList();
            var query = _set.AsNoTracking().Where(x => langList.Contains(x.Lang));
            if (poiIds != null)
            {
                var ids = poiIds.ToList();
                query = query.Where(x => ids.Contains(x.PoiId));
            }
            return await query.ToListAsync();
        }

        public async Task<List<PoiLocalization>> ListByStatusAsync(params LocalizationStatus[] statuses)
            => await _set.AsNoTracking().Where(x => statuses.Contains(x.Status)).ToListAsync();

        /*
         * Thêm mới nếu chưa có bản dịch (PoiId, Lang), có rồi thì sửa.
         * apply = hàm gán các trường cần đổi. Hai tác vụ nền cùng thêm 1 lúc -> bị ràng buộc UNIQUE chặn,
         * khi đó đọc lại bản ghi vừa được thêm rồi sửa.
         */
        public async Task<PoiLocalization> UpsertAsync(int poiId, string lang, Action<PoiLocalization> apply)
        {
            var loc = await _set.FirstOrDefaultAsync(x => x.PoiId == poiId && x.Lang == lang);
            if (loc != null)
            {
                apply(loc);
                loc.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return loc;
            }

            loc = new PoiLocalization { PoiId = poiId, Lang = lang };
            apply(loc);
            loc.UpdatedAt = DateTime.UtcNow;
            _set.Add(loc);
            try
            {
                await _context.SaveChangesAsync();
                return loc;
            }
            catch (DbUpdateException)
            {
                _context.Entry(loc).State = EntityState.Detached;
                var existing = await _set.FirstOrDefaultAsync(x => x.PoiId == poiId && x.Lang == lang);
                if (existing == null) throw; // lỗi khác (vd điểm vừa bị xóa) -> báo lên
                apply(existing);
                existing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return existing;
            }
        }

        // Đổi trạng thái mọi bản dịch của 1 POI bằng 1 câu UPDATE
        public async Task<int> MarkStatusForPoiAsync(int poiId, LocalizationStatus status)
        {
            var now = DateTime.UtcNow;
            return await _set.Where(x => x.PoiId == poiId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status).SetProperty(x => x.UpdatedAt, now));
        }

        public async Task<int> CountForPoiAsync(int poiId)
            => await _set.CountAsync(x => x.PoiId == poiId);

        public async Task<int> DeleteForPoiAsync(int poiId)
            => await _set.Where(x => x.PoiId == poiId).ExecuteDeleteAsync();

        public async Task<bool> DeleteOneAsync(int poiId, string lang)
            => await _set.Where(x => x.PoiId == poiId && x.Lang == lang).ExecuteDeleteAsync() == 1;

        public async Task<Dictionary<string, int>> CountByStatusAsync()
            => await _set.GroupBy(x => x.Status)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count);

        public async Task<DateTime?> LatestUpdateAsync()
            => await _set.MaxAsync(x => (DateTime?)x.UpdatedAt);
    }
}
