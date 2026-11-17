using AudioGuide.Api.Data;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Models;
using AudioGuide.Api.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace AudioGuide.Api.Repositories
{
    public class PoiRepository : Repository<Poi>, IPoiRepository
    {
        public PoiRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Poi?> GetByCodeAsync(string code)
            => await _set.AsNoTracking().FirstOrDefaultAsync(p => p.Code == code);

        // Lấy 1 POI kèm các khung giờ mở cửa (có theo dõi -> sửa xong gọi UpdateAsync)
        public async Task<Poi?> GetWithDetailsAsync(int id)
            => await _set.Include(p => p.OpeningHours).FirstOrDefaultAsync(p => p.Id == id);

        // Các điểm đang hiển thị cho du khách, theo thứ tự admin sắp xếp
        public async Task<List<Poi>> ListActiveAsync()
            => await _set.AsNoTracking()
                .Include(p => p.OpeningHours)
                .Where(p => p.IsActive)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
                .ToListAsync();

        public async Task<List<Poi>> ListAllAsync()
            => await _set.AsNoTracking()
                .Include(p => p.OpeningHours)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
                .ToListAsync();

        // Tìm theo tên / mã + lọc trạng thái + phân trang (Skip/Take = OFFSET/FETCH trong SQL Server)
        public async Task<(List<Poi> Items, int Total)> SearchAsync(string? keyword, bool? isActive, int skip, int take)
        {
            var query = _set.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var k = keyword.Trim();
                query = query.Where(p => p.Name.Contains(k) || p.Code.Contains(k));
            }
            if (isActive.HasValue)
                query = query.Where(p => p.IsActive == isActive.Value);

            var total = await query.CountAsync();
            var items = await query
                .Include(p => p.OpeningHours)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
                .Skip(skip).Take(take)
                .ToListAsync();
            return (items, total);
        }

        public async Task<List<int>> GetMissingIdsAsync(IEnumerable<int> ids)
        {
            var wanted = ids.Distinct().ToList();
            if (wanted.Count == 0) return new List<int>();
            var found = await _set.Where(p => wanted.Contains(p.Id)).Select(p => p.Id).ToListAsync();
            return wanted.Except(found).ToList();
        }

        public async Task<int> CountActiveAsync()
            => await _set.CountAsync(p => p.IsActive);

        public async Task<DateTime?> LatestUpdateAsync()
            => await _set.MaxAsync(p => (DateTime?)p.UpdatedAt);
    }
}
