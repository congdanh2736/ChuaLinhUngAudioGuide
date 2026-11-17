using AudioGuide.Api.Data;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Models;
using AudioGuide.Api.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace AudioGuide.Api.Repositories
{
    public class TourRepository : Repository<Tour>, ITourRepository
    {
        public TourRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Tour?> GetByCodeAsync(string code)
            => await _set.AsNoTracking().FirstOrDefaultAsync(t => t.Code == code);

        // Lộ trình kèm danh sách điểm (bảng TourPois) và bản dịch (bảng TourTranslations)
        public async Task<Tour?> GetWithDetailsAsync(int id)
            => await _set
                .Include(t => t.TourPois)
                .Include(t => t.Translations)
                .AsSplitQuery()
                .FirstOrDefaultAsync(t => t.Id == id);

        public async Task<List<Tour>> ListWithDetailsAsync(bool activeOnly)
            => await _set.AsNoTracking()
                .Include(t => t.TourPois)
                .Include(t => t.Translations)
                .AsSplitQuery()
                .Where(t => !activeOnly || t.IsActive)
                .OrderBy(t => t.Name)
                .ToListAsync();

        /*
         * Gỡ 1 điểm khỏi mọi lộ trình rồi đánh số lại thứ tự các điểm còn lại (0, 1, 2...).
         * Khóa chính của TourPois là (TourId, Position) nên xóa hết rồi thêm lại trong cùng 1 giao dịch.
         */
        public async Task<int> RemovePoiEverywhereAsync(int poiId)
        {
            var tours = await _set.Include(t => t.TourPois)
                .Where(t => t.TourPois.Any(tp => tp.PoiId == poiId))
                .ToListAsync();
            foreach (var tour in tours)
            {
                var remaining = tour.TourPois.OrderBy(tp => tp.Position)
                    .Where(tp => tp.PoiId != poiId)
                    .Select(tp => tp.PoiId)
                    .ToList();
                _context.TourPois.RemoveRange(tour.TourPois);
                await _context.SaveChangesAsync();
                for (var i = 0; i < remaining.Count; i++)
                    _context.TourPois.Add(new TourPoi { TourId = tour.Id, Position = i, PoiId = remaining[i] });
                tour.UpdatedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
            return tours.Count;
        }

        public async Task<DateTime?> LatestUpdateAsync()
            => await _set.MaxAsync(t => (DateTime?)t.UpdatedAt);
    }
}
