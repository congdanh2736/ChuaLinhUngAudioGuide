using AudioGuide.Api.Data;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Models;
using AudioGuide.Api.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace AudioGuide.Api.Repositories
{
    public class LanguageRepository : Repository<Language>, ILanguageRepository
    {
        public LanguageRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Language?> GetByCodeAsync(string code)
            => await _set.FirstOrDefaultAsync(l => l.Code == code);

        // Danh sách ngôn ngữ theo thứ tự hiển thị
        public async Task<List<Language>> ListAsync(bool activeOnly)
            => await _set.AsNoTracking()
                .Where(l => !activeOnly || l.IsActive)
                .OrderBy(l => l.SortOrder)
                .ToListAsync();

        public async Task<int> CountActiveAsync()
            => await _set.CountAsync(l => l.IsActive);
    }
}
