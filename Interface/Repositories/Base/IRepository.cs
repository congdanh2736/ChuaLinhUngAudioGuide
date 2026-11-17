namespace AudioGuide.Api.Interface.Repositories.Base
{
    // Các thao tác CRUD chung cho mọi bảng (lớp 3 - Data Access)
    public interface IRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetAllAsync();
        Task<T?> GetByIdAsync(int id);
        Task<int> CountAsync();
        Task AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
    }
}
