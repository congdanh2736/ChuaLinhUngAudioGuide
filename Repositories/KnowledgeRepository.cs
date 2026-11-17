using AudioGuide.Api.Data;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Models;
using AudioGuide.Api.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace AudioGuide.Api.Repositories
{
    public class KnowledgeRepository : Repository<KnowledgeArticle>, IKnowledgeRepository
    {
        public KnowledgeRepository(ApplicationDbContext context) : base(context) { }

        public async Task<List<KnowledgeArticle>> ListAsync(bool activeOnly)
            => await _set.AsNoTracking()
                .Where(a => !activeOnly || a.IsActive)
                .OrderBy(a => a.Title)
                .ToListAsync();
    }
}
