using MediatR;
using TechStore.Shared.DTOs;

namespace TechStore.Application.Features.Orders.Queries
{
    public class GetAllOrdersQuery : IRequest<List<OrderDto>>
    {
    }

    public class GetOrderByIdQuery : IRequest<OrderDto?>
    {
        public int Id { get; set; }
        public GetOrderByIdQuery(int id) => Id = id;
    }

    public class GetOrdersByUserIdQuery : IRequest<List<OrderDto>>
    {
        public string UserId { get; set; } = string.Empty;
        public GetOrdersByUserIdQuery(string userId) => UserId = userId;
    }
}
