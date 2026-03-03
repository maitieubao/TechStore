using MediatR;
using TechStore.Shared.DTOs;

namespace TechStore.Application.Features.Products.Queries
{
    public class GetFilteredProductsQuery : IRequest<PagedResultDto<ProductDto>>
    {
        public ProductFilterDto Filter { get; set; } = new();
    }
}
