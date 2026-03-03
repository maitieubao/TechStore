using System.Linq.Expressions;

namespace TechStore.Application.Common.Specifications
{
    public interface ISpecification<T> where T : class
    {
        // Filtering
        Expression<Func<T, bool>>? Criteria { get; }

        // Includes (eager loading)
        List<Expression<Func<T, object>>> Includes { get; }

        // String-based includes (for ThenInclude chains)
        List<string> IncludeStrings { get; }

        // Ordering
        Expression<Func<T, object>>? OrderBy { get; }
        Expression<Func<T, object>>? OrderByDescending { get; }

        // Paging
        int Skip { get; }
        int Take { get; }
        bool IsPagingEnabled { get; }
    }
}
