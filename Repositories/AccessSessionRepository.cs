using AudioGuide.Api.Data;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Models;
using AudioGuide.Api.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace AudioGuide.Api.Repositories
{
    public class AccessSessionRepository : Repository<AccessSession>, IAccessSessionRepository
    {
        public AccessSessionRepository(ApplicationDbContext context) : base(context) { }

        public async Task<AccessSession?> GetByCodeAsync(string code)
            => await _set.FirstOrDefaultAsync(s => s.Code == code);

        public async Task<bool> CodeExistsAsync(string code)
            => await _set.AnyAsync(s => s.Code == code);

        public async Task<(List<AccessSession> Items, int Total)> SearchAsync(AccessStatus? status, int skip, int take)
        {
            var query = _set.AsNoTracking().Where(s => status == null || s.Status == status);
            var total = await query.CountAsync();
            var items = await query.OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
                .Skip(skip).Take(take).ToListAsync();
            return (items, total);
        }

        /*
         * Chuyển sang Expired mọi phiên đã quá hạn bằng 1 câu UPDATE:
         * - Active: quá hạn sử dụng (AccessExpiresAt)
         * - Paid  : chưa đổi mã mà quá hạn đổi mã (CodeExpiresAt)
         */
        public async Task<int> MarkExpiredAsync(DateTime now)
            => await _set
                .Where(s => (s.Status == AccessStatus.Active && s.AccessExpiresAt != null && s.AccessExpiresAt < now)
                         || (s.Status == AccessStatus.Paid && s.CodeExpiresAt < now))
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.Status, AccessStatus.Expired));

        public async Task<Dictionary<string, int>> CountByStatusAsync()
            => await _set.GroupBy(s => s.Status)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count);

        // Doanh thu = tổng tiền các phiên đã thanh toán (kể cả đã dùng / đã hết hạn)
        public async Task<long> TotalRevenueAsync()
            => await _set
                .Where(s => s.Status == AccessStatus.Paid || s.Status == AccessStatus.Active || s.Status == AccessStatus.Expired)
                .SumAsync(s => (long)s.Amount);
    }
}
