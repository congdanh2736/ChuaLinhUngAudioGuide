using AudioGuide.Api.Data;
using AudioGuide.Api.Interface.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace AudioGuide.Api.Repositories.Base
{
    /*
     * Repository cơ sở: viết 1 lần các thao tác CRUD chung, các repository cụ thể kế thừa lại.
     * Repository là nơi DUY NHẤT được nói chuyện với database (qua ApplicationDbContext của EF Core).
     */
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _set;

        public Repository(ApplicationDbContext context)
        {
            _context = context;
            _set = context.Set<T>();
        }

        // Chỉ đọc để hiển thị -> AsNoTracking cho nhẹ
        public virtual async Task<IEnumerable<T>> GetAllAsync()
            => await _set.AsNoTracking().ToListAsync();

        // FindAsync: lấy bản ghi theo khóa chính và THEO DÕI thay đổi -> sửa xong gọi UpdateAsync là lưu
        public virtual async Task<T?> GetByIdAsync(int id)
            => await _set.FindAsync(id);

        public virtual async Task<int> CountAsync()
            => await _set.CountAsync();

        public virtual async Task AddAsync(T entity)
        {
            await _set.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public virtual async Task UpdateAsync(T entity)
        {
            // Bản ghi lấy bằng GetByIdAsync đã được theo dõi -> chỉ cần SaveChanges
            if (_context.Entry(entity).State == EntityState.Detached)
                _set.Update(entity);
            await _context.SaveChangesAsync();
        }

        public virtual async Task DeleteAsync(T entity)
        {
            _set.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}
