using MediatR;
using TechStore.Shared.DTOs;

namespace TechStore.Application.Features.Products.Queries
{
    public class GetProductByIdQuery : IRequest<ProductDto?>
    {
        public int Id { get; set; }
        public GetProductByIdQuery(int id) => Id = id;
    }
}
