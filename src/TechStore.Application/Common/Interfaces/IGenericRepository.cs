using System.Linq.Expressions;
using TechStore.Application.Common.Specifications;

namespace TechStore.Application.Common.Interfaces
{
    public interface IGenericRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(int id);
        Task<List<T>> GetAllAsync();
        Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate);

        // Count
        Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);

        // IQueryable for complex queries
        IQueryable<T> Query();

        // Paging support
        Task<(List<T> Items, int TotalCount)> GetPagedAsync(
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            int page = 1,
            int pageSize = 12,
            params Expression<Func<T, object>>[] includes);

        // Specification Pattern support
        Task<List<T>> ListAsync(ISpecification<T> spec);
        Task<int> CountAsync(ISpecification<T> spec);

        // Any
        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);

        // FirstOrDefault
        Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);

        Task AddAsync(T entity);
        Task AddRangeAsync(IEnumerable<T> entities);
        void Update(T entity);
        void Delete(T entity);
        void DeleteRange(IEnumerable<T> entities);
    }
}
