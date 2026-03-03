using MediatR;
using TechStore.Shared.DTOs;

namespace TechStore.Application.Features.Orders.Commands
{
    public class CreateOrderCommand : IRequest<OrderDto>
    {
        public string UserId { get; set; } = string.Empty;
        public List<CreateOrderItemDto> Items { get; set; } = new();
        public string? ShippingAddress { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Note { get; set; }
        public string? CouponCode { get; set; }
    }
}
