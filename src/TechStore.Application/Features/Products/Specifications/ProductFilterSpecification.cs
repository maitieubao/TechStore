using System.Linq.Expressions;
using TechStore.Application.Common.Specifications;
using TechStore.Domain.Entities;
using TechStore.Shared.DTOs;

namespace TechStore.Application.Features.Products.Specifications
{
    public class ProductFilterSpecification : BaseSpecification<Product>
    {
        public ProductFilterSpecification(ProductFilterDto filter)
        {
            // Build criteria from filter parameters
            var predicates = new List<Expression<Func<Product, bool>>>();

            // Lọc theo IsActive (Admin để null sẽ thấy hết, Khách mặc định thấy True)
            if (filter.IsActive.HasValue)
                predicates.Add(p => p.IsActive == filter.IsActive.Value);

            // Search
            if (!string.IsNullOrEmpty(filter.Search))
            {
                var search = filter.Search.ToLower();
                predicates.Add(p =>
                    p.Name.ToLower().Contains(search)
                    || p.Description.ToLower().Contains(search)
                    || (p.Brand != null && p.Brand.ToLower().Contains(search))
                    || (p.Specifications != null && p.Specifications.ToLower().Contains(search)));
            }

            // Category
            if (filter.CategoryId.HasValue)
                predicates.Add(p => p.CategoryId == filter.CategoryId.Value);

            if (!string.IsNullOrEmpty(filter.CategorySlug))
                predicates.Add(p => p.Category != null && p.Category.Slug == filter.CategorySlug);

            // Brand
            if (!string.IsNullOrEmpty(filter.Brand))
                predicates.Add(p => p.Brand == filter.Brand);

            // Price range
            if (filter.MinPrice.HasValue)
                predicates.Add(p => p.Price >= filter.MinPrice.Value);

            if (filter.MaxPrice.HasValue)
                predicates.Add(p => p.Price <= filter.MaxPrice.Value);

            // In stock
            if (filter.InStock == true)
                predicates.Add(p => p.StockQuantity > 0);

            // Combo filter
            if (filter.IsCombo.HasValue)
                predicates.Add(p => p.IsCombo == filter.IsCombo.Value);

            // Rating
            if (filter.MinRating.HasValue)
                predicates.Add(p => p.Reviews.Any() && p.Reviews.Average(r => r.Rating) >= filter.MinRating.Value);

            // Combine all predicates with AND
            var combined = CombinePredicates(predicates);
            if (combined != null)
                SetCriteria(combined);

            // Includes  
            AddInclude(p => p.Category!);
            AddInclude(p => p.Images);
            AddInclude(p => p.Reviews);

            // Sorting
            switch (filter.SortBy?.ToLower())
            {
                case "price":
                    if (filter.SortDescending)
                        ApplyOrderByDescending(p => p.Price);
                    else
                        ApplyOrderBy(p => p.Price);
                    break;
                case "name":
                    if (filter.SortDescending)
                        ApplyOrderByDescending(p => p.Name);
                    else
                        ApplyOrderBy(p => p.Name);
                    break;
                case "newest":
                    ApplyOrderByDescending(p => p.CreatedAt);
                    break;
                default:
                    ApplyOrderByDescending(p => p.CreatedAt);
                    break;
            }

            // Paging
            ApplyPaging((filter.Page - 1) * filter.PageSize, filter.PageSize);
        }

        /// <summary>
        /// Combines multiple predicates into a single AND expression
        /// </summary>
        private static Expression<Func<Product, bool>>? CombinePredicates(
            List<Expression<Func<Product, bool>>> predicates)
        {
            if (predicates.Count == 0) return null;

            var combined = predicates[0];
            for (int i = 1; i < predicates.Count; i++)
            {
                var param = Expression.Parameter(typeof(Product), "p");
                var body = Expression.AndAlso(
                    Expression.Invoke(combined, param),
                    Expression.Invoke(predicates[i], param));
                combined = Expression.Lambda<Func<Product, bool>>(body, param);
            }
            return combined;
        }
    }
}
