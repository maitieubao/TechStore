using MediatR;
using TechStore.Shared.DTOs;

namespace TechStore.Application.Features.Products.Queries
{
    public class GetAllProductsQuery : IRequest<List<ProductDto>>
    {
    }
}
